#!/usr/bin/env node
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

type JsonObject = Record<string, unknown>;
type Classification = 'variable' | 'secret';
type ValueType = 'string' | 'int' | 'bool';
type RequiredWhen = 'always' | 'deploy_infra' | 'existing_infra' | 'never';
type EmitWhen = 'always' | 'nonEmpty';

interface DeploymentSetting {
  id: string;
  githubName: string;
  appServiceName: string;
  targets: ('web' | 'functions')[];
  description: string;
  classification: Classification;
  valueType: ValueType;
  requiredWhen: RequiredWhen;
  emitWhen?: EmitWhen;
  defaultValue?: string;
}

interface DeploymentSettingsContract {
  version: 1;
  settings: DeploymentSetting[];
}

interface DeploymentSettingsOverlay {
  version: 1;
  disabled: string[];
  overrides: Record<string, JsonObject>;
  additions: JsonObject[];
}

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const generatedTargets = [
  '.github/workflows/configure-azure.yml',
  'infrastructure/azure/deployment-settings.generated.json',
];

const allowedClassifications = new Set(['variable', 'secret']);
const allowedValueTypes = new Set(['string', 'int', 'bool']);
const allowedRequiredWhen = new Set(['always', 'deploy_infra', 'existing_infra', 'never']);
const allowedEmitWhen = new Set(['always', 'nonEmpty']);
const allowedOverrideKeys = new Set([
  'targets',
  'classification',
  'valueType',
  'description',
  'requiredWhen',
  'emitWhen',
  'appServiceName',
  'defaultValue',
]);

function fail(message: string): never {
  throw new Error(message);
}

function assertString(value: unknown, label: string): asserts value is string {
  if (typeof value !== 'string' || value.length === 0) {
    fail(`${label} must be a non-empty string.`);
  }
}

function assertPlainObject(value: unknown, label: string): asserts value is JsonObject {
  if (!isPlainObject(value)) {
    fail(`${label} must be an object.`);
  }
}

function isPlainObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function deepMerge(base: unknown, overlay: unknown): unknown {
  if (!isPlainObject(base) || !isPlainObject(overlay)) {
    return clone(overlay);
  }

  const result: JsonObject = clone(base);
  for (const [key, value] of Object.entries(overlay)) {
    result[key] = key in result ? deepMerge(result[key], value) : clone(value);
  }
  return result;
}

function githubNameToId(githubName: string): string {
  return githubName.toLowerCase().replace(/_+/g, '-');
}

function replaceBlock(content: string, id: string, generatedLines: string[]): string {
  const escapedId = id.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const pattern = new RegExp(`(^[ \\t]*)# <deployment-settings:${escapedId}>\\n[\\s\\S]*?^[ \\t]*# </deployment-settings:${escapedId}>`, 'm');
  const match = content.match(pattern);

  if (!match) {
    fail(`Missing generated block marker for ${id}.`);
  }

  const indent = match[1] ?? '';
  const body = generatedLines.map((line) => (line.length === 0 ? '' : `${indent}${line}`)).join('\n');
  const replacement = [`${indent}# <deployment-settings:${id}>`, body, `${indent}# </deployment-settings:${id}>`]
    .filter((line) => line !== '')
    .join('\n');

  return content.replace(match[0], replacement);
}

function sortSettings(settings: DeploymentSetting[]): DeploymentSetting[] {
  return [...settings].sort((left, right) => left.githubName.localeCompare(right.githubName));
}

function validateSettingsArrayContract(contract: unknown, label: string): asserts contract is { settings: unknown[] } {
  assertPlainObject(contract, label);
  if (!Array.isArray(contract.settings)) {
    fail(`${label} must contain a settings array.`);
  }
}

function validateContract(contract: unknown): asserts contract is DeploymentSettingsContract {
  validateSettingsArrayContract(contract, 'Resolved deployment settings');
  if ((contract as JsonObject).version !== 1) fail('Settings version must be 1.');

  const ids = new Set<string>();
  const githubNames = new Map<string, string>();
  const appServiceNames = new Map<string, string>();

  for (const setting of contract.settings) {
    assertPlainObject(setting, 'setting');
    assertString(setting.id, 'setting.id');
    assertString(setting.githubName, `${setting.id}.githubName`);
    assertString(setting.appServiceName, `${setting.githubName}.appServiceName`);
    if (ids.has(setting.id)) fail(`Duplicate setting id ${setting.id}.`);
    ids.add(setting.id);
    if (!Array.isArray(setting.targets) || setting.targets.length === 0 || setting.targets.some(t => t !== 'web' && t !== 'functions') || new Set(setting.targets).size !== setting.targets.length) fail(`${setting.id} requires distinct web/functions targets.`);
    if (isPlatformSetting(setting.appServiceName)) fail(`${setting.appServiceName} is infrastructure-owned.`);
    if (/^(GITHUB_|RUNNER_)/.test(setting.githubName)) fail(`${setting.githubName} is reserved by GitHub.`);
    if (['PATH', 'HOME', 'NODE_OPTIONS', 'NODE_PATH', 'DEPLOY_ENV', 'DEPLOY_INFRA', 'APP_NAME', 'RESOURCE_GROUP', 'AZURE_CLIENT_ID', 'AZURE_TENANT_ID', 'AZURE_SUBSCRIPTION_ID', 'AZURE_LOCATION', 'DEPLOYMENT_NAME', 'WEB_PLAN_NAME', 'WEB_PLAN_RESOURCE_GROUP', 'SQL_ADMIN_LOGIN', 'SQL_ADMIN_PASSWORD', 'SQL_DATABASE_NAME', 'SQL_SKU_NAME', 'SQL_SKU_TIER'].includes(setting.githubName)) fail(`${setting.githubName} is reserved by deployment tooling.`);
    assertString(setting.description, `${setting.githubName}.description`);

    if (typeof setting.classification !== 'string' || !allowedClassifications.has(setting.classification)) {
      fail(`${setting.githubName} has invalid classification '${setting.classification}'.`);
    }

    if (typeof setting.valueType !== 'string' || !allowedValueTypes.has(setting.valueType)) {
      fail(`${setting.githubName} has invalid valueType '${setting.valueType}'.`);
    }

    if (typeof setting.requiredWhen !== 'string' || !allowedRequiredWhen.has(setting.requiredWhen)) {
      fail(`${setting.githubName} has invalid requiredWhen '${setting.requiredWhen}'.`);
    }

    if (!/^[A-Z][A-Z0-9_]*$/.test(setting.githubName)) {
      fail(`${setting.githubName} must use upper snake case.`);
    }

    const emitWhen = setting.emitWhen ?? 'nonEmpty';
    if (typeof emitWhen !== 'string' || !allowedEmitWhen.has(emitWhen)) {
      fail(`${setting.githubName} has invalid emitWhen '${emitWhen}'.`);
    }

    if (setting.defaultValue !== undefined && typeof setting.defaultValue !== 'string') {
      fail(`${setting.githubName} has invalid defaultValue; defaultValue must be a string.`);
    }

    if (githubNames.has(setting.githubName)) {
      fail(`Duplicate GitHub setting name '${setting.githubName}' in ${githubNames.get(setting.githubName)} and ${setting.id}.`);
    }
    githubNames.set(setting.githubName, setting.id);

    for (const target of setting.targets) {
      const key = `${target}:${setting.appServiceName.toLowerCase()}`;
      if (appServiceNames.has(key)) fail(`Duplicate App Service setting ${key}.`);
      appServiceNames.set(key, setting.id);
    }
  }
}

function validateOverlay(overlay: unknown, defaults: DeploymentSettingsContract): asserts overlay is DeploymentSettingsOverlay {
  assertPlainObject(overlay, 'deployment-settings.json');

  if (overlay.version !== 1) {
    fail('deployment-settings.json version must be 1.');
  }

  if (!Array.isArray(overlay.disabled)) {
    fail('deployment-settings.json disabled must be an array.');
  }

  assertPlainObject(overlay.overrides, 'deployment-settings.json overrides');

  if (!Array.isArray(overlay.additions)) {
    fail('deployment-settings.json additions must be an array.');
  }

  const defaultSettings = new Map(defaults.settings.map((setting) => [setting.githubName, setting]));
  const seenDisabled = new Set<string>();

  for (const githubName of overlay.disabled) {
    assertString(githubName, 'deployment-settings.json disabled entry');
    if (seenDisabled.has(githubName)) {
      fail(`deployment-settings.json disables ${githubName} more than once.`);
    }
    seenDisabled.add(githubName);
    if (!defaultSettings.has(githubName)) {
      fail(`deployment-settings.json disables unknown default setting ${githubName}.`);
    }
  }

  for (const [githubName, override] of Object.entries(overlay.overrides)) {
    if (!defaultSettings.has(githubName)) {
      fail(`deployment-settings.json overrides unknown default setting ${githubName}.`);
    }

    if (seenDisabled.has(githubName)) {
      fail(`deployment-settings.json cannot both disable and override ${githubName}.`);
    }

    assertPlainObject(override, `deployment-settings.json overrides.${githubName}`);
    for (const key of Object.keys(override)) {
      if (key === 'id' || key === 'githubName') {
        fail(`deployment-settings.json overrides.${githubName} cannot change id or githubName; use additions for new settings.`);
      }

      if (!allowedOverrideKeys.has(key)) {
        fail(
          `deployment-settings.json overrides.${githubName} cannot use unsupported property '${key}'; permitted keys are targets, classification, valueType, description, requiredWhen, emitWhen, appServiceName, and defaultValue.`,
        );
      }
    }
  }

  for (const [index, addition] of overlay.additions.entries()) {
    assertPlainObject(addition, `deployment-settings.json additions[${index}]`);
  }
}

function normalizeAddition(addition: JsonObject): DeploymentSetting {
  const normalized = clone(addition);
  assertString(normalized.githubName, 'addition.githubName');

  normalized.id ??= githubNameToId(normalized.githubName);
  normalized.requiredWhen ??= 'never';
  normalized.emitWhen ??= 'nonEmpty';

  return normalized as unknown as DeploymentSetting;
}

function validateAdditionCollisions(defaults: DeploymentSettingsContract, additions: DeploymentSetting[]): void {
  const defaultGithubNames = new Set(defaults.settings.map((setting) => setting.githubName));
  for (const addition of additions) {
    if (defaultGithubNames.has(addition.githubName)) fail(`${addition.githubName} is a built-in; use overrides instead.`);
  }
}

export function resolveSettings(defaults: unknown, overlay: unknown): DeploymentSettingsContract {
  validateSettingsArrayContract(defaults, 'deployment-settings-defaults.json');
  validateContract(defaults);
  validateOverlay(overlay, defaults);

  const disabled = new Set(overlay.disabled);
  const settings = defaults.settings
    .filter((setting) => !disabled.has(setting.githubName))
    .map((setting) => deepMerge(setting, overlay.overrides[setting.githubName] ?? {}) as DeploymentSetting);
  const additions = overlay.additions.map(normalizeAddition);

  validateAdditionCollisions(defaults, additions);

  const resolved = {
    version: 1,
    settings: [...settings, ...additions],
  };
  validateContract(resolved);
  return resolved;
}

function contractSettings(contract: DeploymentSettingsContract): DeploymentSetting[] {
  return sortSettings(contract.settings);
}


export function isPlatformSetting(name: string): boolean {
  return /^(ConnectionStrings(:|__)|SQLAZURECONNSTR_|WEBSITE|FUNCTIONS_|AZURE_FUNCTIONS_|ASPNETCORE_|APPLICATIONINSIGHTS_|APPINSIGHTS_|AzureWebJobsStorage$|Azure(:|__)SubscriptionId$|Serilog(:|__)Environment$|DB_CONNECTION$)/i.test(name);
}

export function runtimePayload(settings: DeploymentSetting[], env: NodeJS.ProcessEnv, deployInfra: boolean) {
  const payload: Record<'web' | 'functions', Record<string, string>> = { web: {}, functions: {} };
  for (const setting of settings) {
    const value = env[setting.githubName] || setting.defaultValue || '';
    const required = setting.requiredWhen === 'always' || setting.requiredWhen === (deployInfra ? 'deploy_infra' : 'existing_infra');
    if (required && !value.trim()) fail(`${setting.githubName} is required.`);
    if (value && setting.valueType === 'bool' && !/^(true|false)$/.test(value)) fail(`${setting.githubName} must be true or false.`);
    if (value && setting.valueType === 'int' && !/^-?\d+$/.test(value)) fail(`${setting.githubName} must be an integer.`);
    if (value || setting.emitWhen === 'always') {
      for (const target of setting.targets) payload[target][setting.appServiceName] = value;
    }
  }
  return payload;
}

export async function readSettings(root = repoRoot) {
  const defaults = JSON.parse(await readFile(path.join(root, 'infrastructure/azure/deployment-settings-defaults.json'), 'utf8'));
  const overlay = JSON.parse(await readFile(path.join(root, 'infrastructure/azure/deployment-settings.json'), 'utf8'));
  return contractSettings(resolveSettings(defaults, overlay));
}

async function main() {
  const args = process.argv.slice(2);
  if (args.length !== 1 || !['--write', '--check'].includes(args[0]!)) fail('Specify --write or --check.');
  const settings = await readSettings();
  const workflow = path.join(repoRoot, generatedTargets[0]!);
  const content = await readFile(workflow, 'utf8');
  const rendered = replaceBlock(content, 'workflow-env', settings.map(s => `${s.githubName}: \${{ ${s.classification === 'secret' ? 'secrets' : 'vars'}.${s.githubName} }}`));
  const outputs = new Map([[workflow, rendered], [path.join(repoRoot, generatedTargets[1]!), JSON.stringify({ version: 1, settings }, null, 2) + '\n']]);
  for (const [file, output] of outputs) {
    const current = await readFile(file, 'utf8').catch(() => '');
    if (output !== current) {
      if (args[0] === '--check') fail(`Generated settings are stale: ${path.relative(repoRoot, file)}. Run npm run deployment-settings:sync.`);
      await writeFile(file, output);
    }
  }
  console.log(`Validated ${settings.length} deployment settings.`);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch(error => { console.error(error.message); process.exitCode = 1; });
}
