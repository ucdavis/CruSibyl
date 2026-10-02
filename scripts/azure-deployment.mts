import { execFileSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';
import { readSettings, runtimePayload } from './sync-deployment-settings.mts';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const env = process.env;
function required(key: string): string {
  const value = env[key]?.trim();
  if (!value) throw new Error(`${key} is required.`);
  return value;
}

export function validateTarget(environment: string, group: string, expected: string, actual: string) {
  if (!['test', 'prod'].includes(environment)) throw new Error('DEPLOY_ENV must be test or prod.');
  if (!expected || expected.toLowerCase() !== actual.toLowerCase()) throw new Error('Azure subscription mismatch.');
  if (!group.toLowerCase().endsWith(`-${environment}`)) throw new Error('Resource group environment suffix mismatch.');
}

// Capture Azure output: commands can include settings, and CLI errors can echo their arguments.
function az(args: string[], timeout = 60000): any {
  try {
    const output = execFileSync('az', [...args, '--only-show-errors', '--output', 'json'], {
      encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 16 * 1024 * 1024, timeout,
    });
    return output.trim() ? JSON.parse(output) : null;
  } catch {
    throw new Error(`Azure CLI operation failed: ${args.slice(0, 3).join(' ')}. Inspect Azure deployment diagnostics; sensitive output was suppressed.`);
  }
}

interface Site { name: string; kind: string; tags?: Record<string, string> }
export function selectApps(sites: Site[], appName: string, environment: string, optional = false) {
  const matching = sites.filter(s => s.tags?.application === appName && s.tags?.environment === environment);
  const resolve = (functions: boolean) => {
    const candidates = matching.filter(s => s.kind?.split(',').includes('functionapp') === functions);
    if (candidates.length > 1 || (!optional && candidates.length !== 1)) throw new Error(`Expected exactly one tagged ${functions ? 'Function' : 'Web'} App.`);
    return candidates[0]?.name;
  };
  return { web: resolve(false), functions: resolve(true) };
}

export const expectedFunctions = ['AppWebJobSyncFunction', 'ManifestSyncFunction', 'PackageVersionSyncFunction', 'WebJobStatusSyncFunction']
  .flatMap(name => [name, `${name}_Http`]);

export function preserveSettings(entries: { name: string; value: string }[], target: 'web' | 'functions') {
  // Only omit values actually replaced by compute.bicep, not arbitrary platform settings.
  const replaced = new Set(['website_run_from_package', 'serilog__environment', 'azure__subscriptionid',
    'applicationinsights_connection_string', 'appinsights_instrumentationkey',
    ...(target === 'web' ? ['aspnetcore_environment', 'websites_container_start_time_limit']
      : ['azure_functions_environment', 'azurewebjobsstorage', 'functions_extension_version', 'functions_worker_runtime'])]);
  return Object.fromEntries(entries.filter(s => !replaced.has(s.name.toLowerCase()) &&
    !/^ConnectionStrings(:|__)DefaultConnection$/i.test(s.name)).map(s => [s.name, s.value]));
}

async function main() {
  const command = process.argv[2];
  if (!['configure', 'deploy', 'verify'].includes(command ?? '')) throw new Error('Use configure, deploy, or verify.');
  const environment = required('DEPLOY_ENV');
  const appName = env.APP_NAME || 'crusibyl';
  const group = env.RESOURCE_GROUP || `rg-${appName}-${environment}`;
  const subscription = required('AZURE_SUBSCRIPTION_ID');
  validateTarget(environment, group, subscription, az(['account', 'show']).id);
  const deployment = env.DEPLOYMENT_NAME || `${appName}-${environment}`;
  const listApps = (optional = false) => selectApps(az(['resource', 'list', '-g', group, '--resource-type', 'Microsoft.Web/sites']), appName, environment, optional);
  // The privileged OIDC bootstrap owns group creation.
  az(['group', 'show', '--name', group]);
  const temporary = mkdtempSync(path.join(tmpdir(), 'crusibyl-deploy-'));
  function jsonFile(name: string, value: unknown) {
    const filename = path.join(temporary, name);
    writeFileSync(filename, JSON.stringify(value), { mode: 0o600 });
    return filename;
  }
  try {
    if (command === 'configure') {
      const infraValue = env.DEPLOY_INFRA || 'true';
      if (!['true', 'false'].includes(infraValue)) throw new Error('DEPLOY_INFRA must be true or false.');
      const deployInfra = infraValue === 'true';
      const settings = runtimePayload(await readSettings(), env, deployInfra);
      if (deployInfra) {
        if (!!env.WEB_PLAN_NAME !== !!env.WEB_PLAN_RESOURCE_GROUP) throw new Error('Both shared plan coordinates must be provided together.');
        if (!!env.SQL_SKU_NAME !== !!env.SQL_SKU_TIER) throw new Error('Both SQL SKU name and tier must be provided together.');
        const apps = listApps(true);
        function snapshot(target: 'web' | 'functions', name?: string): Record<string, string> {
          if (!name) return {};
          const entries = az(['webapp', 'config', 'appsettings', 'list', '-g', group, '-n', name]);
          return preserveSettings(entries, target);
        }
        const values: Record<string, unknown> = {
          appName, env: environment, expectedSubscriptionId: subscription,
          sqlAdminLogin: env.SQL_ADMIN_LOGIN || 'sqladmin', sqlAdminPassword: required('SQL_ADMIN_PASSWORD'),
          existingWebSettings: snapshot('web', apps.web), existingFunctionSettings: snapshot('functions', apps.functions),
        };
        for (const [variable, parameter] of Object.entries({ AZURE_LOCATION: 'location', SQL_DATABASE_NAME: 'sqlDatabaseName', SQL_SKU_NAME: 'sqlSkuName', SQL_SKU_TIER: 'sqlSkuTier', WEB_PLAN_NAME: 'webPlanName', WEB_PLAN_RESOURCE_GROUP: 'webPlanResourceGroup' })) {
          if (env[variable]) values[parameter] = env[variable];
        }
        const parameters = Object.fromEntries(Object.entries(values).map(([key, value]) => [key, { value }]));
        const result = az(['deployment', 'group', 'create', '-g', group, '-n', deployment, '--template-file', path.join(root, 'infrastructure/azure/main.bicep'), '--parameters', `@${jsonFile('parameters.json', { parameters })}`], 600000);
        if (result.properties?.outputs?.deploymentGuardPassed?.value !== true) throw new Error('Bicep deployment guard did not pass.');
      }
      const apps = listApps();
      for (const target of ['web', 'functions'] as const) {
        if (Object.keys(settings[target]).length) az(['webapp', 'config', 'appsettings', 'set', '-g', group, '-n', apps[target]!, '--settings', `@${jsonFile(`${target}.json`, settings[target])}`]);
      }
      console.log('Infrastructure/runtime configuration complete. Production SQL connectivity and Function runtime RBAC are operator prerequisites.');
      return;
    }
    const apps = listApps();
    const timeout = Number(env.APP_HEALTH_TIMEOUT_SECONDS || '120');
    if (!Number.isInteger(timeout) || timeout <= 0) throw new Error('APP_HEALTH_TIMEOUT_SECONDS must be a positive integer.');
    if (command === 'deploy') {
      const packages = required('PACKAGE_DIRECTORY');
      az(['webapp', 'deploy', '-g', group, '-n', apps.web!, '--src-path', path.join(packages, 'webapp.zip'), '--type', 'zip'], 600000);
      az(['functionapp', 'deployment', 'source', 'config-zip', '-g', group, '-n', apps.functions!, '--src', path.join(packages, 'functionapp.zip'), '--timeout', '600'], 660000);
    }
    const hostname = az(['webapp', 'show', '-g', group, '-n', apps.web!]).defaultHostName;
    const deadline = Date.now() + timeout * 1000;
    let webHealthy = false;
    let functionsHealthy = false;
    const remainingTimeout = () => Math.max(1, Math.min(10000, deadline - Date.now()));
    do {
      try {
        webHealthy = (await fetch(`https://${hostname}/health`, { redirect: 'manual', signal: AbortSignal.timeout(remainingTimeout()) })).status === 200;
      } catch { webHealthy = false; }
      try {
        const app = az(['functionapp', 'show', '-g', group, '-n', apps.functions!], remainingTimeout());
        const registered = az(['functionapp', 'function', 'list', '-g', group, '-n', apps.functions!], remainingTimeout()).map((f: { name: string }) => f.name.split('/').at(-1));
        functionsHealthy = app.state === 'Running' && expectedFunctions.every(name => registered.includes(name));
      } catch { functionsHealthy = false; }
      if (webHealthy && functionsHealthy) {
        console.log('Web SQL health check and all Function registrations passed. Verify sign-in and actual synchronization separately.');
        return;
      }
      if (Date.now() < deadline) await delay(Math.min(5000, deadline - Date.now()));
    } while (Date.now() < deadline);
    throw new Error(`Deployment verification failed: web=${webHealthy}, function registrations=${functionsHealthy}.`);
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch(error => { console.error(error.message); process.exitCode = 1; });
}
