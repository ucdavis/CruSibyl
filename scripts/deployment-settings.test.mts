import test from 'node:test';
import assert from 'node:assert/strict';
import { resolveSettings, runtimePayload, isPlatformSetting } from './sync-deployment-settings.mts';
import { selectApps, validateTarget, expectedFunctions, preserveSettings } from './azure-deployment.mts';

const setting = { id: 'sample', githubName: 'SAMPLE', appServiceName: 'Sample__Value', description: 'Sample', targets: ['web'], classification: 'secret', valueType: 'string', requiredWhen: 'never' };
const defaults = { version: 1, settings: [setting] };
const overlay = { version: 1, disabled: [], overrides: {}, additions: [] };

test('routes values to targets and preserves literal secret characters', () => {
  const contract = resolveSettings(defaults, { ...overlay, additions: [{ ...setting, id: 'second', githubName: 'SECOND', targets: ['functions'] }] });
  const secret = 'quote"\n$(`do-not-execute`)\\=test';
  const payload = runtimePayload(contract.settings, { SAMPLE: secret, SECOND: 'functions-only' }, true);
  assert.deepEqual(payload, { web: { Sample__Value: secret }, functions: { Sample__Value: 'functions-only' } });
});

test('overrides, defaults, required conditions and disabled entries', () => {
  const contract = resolveSettings(defaults, { ...overlay, overrides: { SAMPLE: { targets: ['web', 'functions'], requiredWhen: 'always', defaultValue: 'fallback' } } });
  assert.equal(runtimePayload(contract.settings, {}, true).functions.Sample__Value, 'fallback');
  const required = resolveSettings(defaults, { ...overlay, overrides: { SAMPLE: { requiredWhen: 'existing_infra' } } });
  assert.throws(() => runtimePayload(required.settings, {}, false), /required/);
  assert.deepEqual(runtimePayload(required.settings, {}, true), { web: {}, functions: {} });
  assert.equal(resolveSettings(defaults, { ...overlay, disabled: ['SAMPLE'] }).settings.length, 0);
});

test('rejects ambiguous mappings, platform keys, targets and unknown overrides', () => {
  assert.throws(() => resolveSettings(defaults, { ...overlay, additions: [{ ...setting, id: 'second', githubName: 'SECOND' }] }), /Duplicate App Service/);
  assert.throws(() => resolveSettings(defaults, { ...overlay, overrides: { SAMPLE: { appServiceName: 'AzureWebJobsStorage' } } }), /infrastructure-owned/);
  assert.throws(() => resolveSettings(defaults, { ...overlay, overrides: { SAMPLE: { targets: [] } } }), /targets/);
  assert.throws(() => resolveSettings(defaults, { ...overlay, overrides: { MISSING: {} } }), /unknown/);
  assert.throws(() => resolveSettings(defaults, { ...overlay, disabled: ['SAMPLE'], overrides: { SAMPLE: {} } }), /both disable and override/);
  for (const name of ['ConnectionStrings__DefaultConnection', 'WEBSITE_RUN_FROM_PACKAGE', 'ASPNETCORE_ENVIRONMENT', 'Serilog__Environment']) assert.ok(isPlatformSetting(name));
});

test('validates types without printing values; explicit empty clears optional setting', () => {
  const contract = resolveSettings(defaults, { ...overlay, overrides: { SAMPLE: { valueType: 'int', emitWhen: 'always' } } });
  assert.throws(() => runtimePayload(contract.settings, { SAMPLE: 'private-secret' }, true), { message: 'SAMPLE must be an integer.' });
  assert.deepEqual(runtimePayload(contract.settings, {}, true), { web: { Sample__Value: '' }, functions: {} });
});

test('deployment guards and app discovery reject wrong environment and ambiguity', () => {
  validateTarget('test', 'rg-crusibyl-test', 'ABC', 'abc');
  assert.throws(() => validateTarget('dev', 'rg-crusibyl-dev', 'abc', 'abc'));
  assert.throws(() => validateTarget('prod', 'rg-crusibyl-test', 'abc', 'abc'));
  assert.throws(() => validateTarget('test', 'rg-crusibyl-test', 'abc', 'other'));
  const tags = { application: 'crusibyl', environment: 'test' };
  const sites = [{ name: 'web', kind: 'app,linux', tags }, { name: 'fn', kind: 'functionapp,linux', tags }];
  assert.deepEqual(selectApps(sites, 'crusibyl', 'test'), { web: 'web', functions: 'fn' });
  assert.throws(() => selectApps([...sites, sites[0]!], 'crusibyl', 'test'));
  assert.throws(() => selectApps(sites, 'crusibyl', 'prod'));
  assert.equal(expectedFunctions.length, 8);
});

test('infra reruns preserve unmanaged runtime and platform settings', () => {
  assert.deepEqual(preserveSettings([
    { name: 'Auth__ClientSecret', value: 'secret' },
    { name: 'WEBSITE_TIME_ZONE', value: 'UTC' },
    { name: 'WEBSITE_RUN_FROM_PACKAGE', value: 'old-value' },
    { name: 'ConnectionStrings__DefaultConnection', value: 'stale' },
    { name: 'DisabledOverlayEntry', value: 'keep' },
  ], 'web'), { Auth__ClientSecret: 'secret', WEBSITE_TIME_ZONE: 'UTC', DisabledOverlayEntry: 'keep' });
});
