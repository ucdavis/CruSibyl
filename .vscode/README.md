# Azure Functions Local Debugging

This workspace keeps VS Code tasks focused on shared setup/build/debug lifecycle tasks. Function-specific trigger tasks were removed to keep `tasks.json` small.

## Recommended Debug Flow

1. Set breakpoints in the timer-triggered function you want to debug.
2. Temporarily set `RunOnStartup = true` on that function's `[TimerTrigger(...)]` attribute.
3. Start **Debug Azure Functions (Start & Attach)**.
4. Select the `dotnet` Functions worker process from the process picker.
5. The selected timer function runs on host startup and should hit your breakpoints.
6. Revert `RunOnStartup` when you finish debugging.

Example:

```csharp
[TimerTrigger("0 0 */4 * * *", RunOnStartup = true)]
```

## Optional Manual HTTP Triggering

If you want to trigger HTTP endpoints directly:

```bash
curl -X POST http://localhost:7071/api/ManifestSyncFunction_Http
curl -X POST http://localhost:7071/api/AppWebJobSyncFunction_Http
curl -X POST http://localhost:7071/api/PackageVersionSyncFunction_Http
curl -X POST http://localhost:7071/api/WebJobStatusSyncFunction_Http
```

The helper script is still available:

```bash
./.vscode/trigger-functions.sh [manifest|webjob|packageversion|webjobstatus|all]
```

## Prerequisites

- Azure Functions Core Tools and Azurite installed
- Functions listen on `http://localhost:7071` by default
- `local.settings.json` will be auto-created from `CruSibyl.Functions/local.settings.json.template` when missing
- `Debug Azure Functions (Start & Attach)` now verifies Azurite ports `10000/10001/10002` before starting the Functions host

## First-Time Local Setup

The Functions host reads app config from `appsettings.json`, environment variables, and user secrets.

```bash
dotnet user-secrets set --project CruSibyl.Functions/CruSibyl.Functions.csproj "ConnectionStrings:DefaultConnection" "Data Source=/tmp/crusibyl.db"
dotnet user-secrets set --project CruSibyl.Functions/CruSibyl.Functions.csproj "GitHub:AccessToken" "<token>"
```

Optional if you want Azure App/WebJob sync calls to return data:

```bash
dotnet user-secrets set --project CruSibyl.Functions/CruSibyl.Functions.csproj "Azure:Subscriptions:CAES-Test:SubscriptionId" "<subscription-guid>"
dotnet user-secrets set --project CruSibyl.Functions/CruSibyl.Functions.csproj "Azure:Subscriptions:CAES-Test:Enabled" "true"
```
