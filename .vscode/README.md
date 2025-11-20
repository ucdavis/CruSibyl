# Azure Functions Testing Scripts

This directory contains helper scripts and HTTP files for triggering Azure Functions during development and debugging.

## Available Functions

1. **ManifestSyncFunction** - Syncs app manifests from Azure
2. **AppWebJobSyncFunction** - Syncs WebJobs from Azure App Services
3. **PackageVersionSyncFunction** - Syncs package versions (NuGet/npm)
4. **WebJobStatusSyncFunction** - Syncs WebJob status updates and logs

## Usage Options

### Option 1: VS Code Tasks (Recommended)

Press `Cmd+Shift+P` → "Run Task" → Select:
- `Trigger: ManifestSync`
- `Trigger: AppWebJobSync`
- `Trigger: PackageVersionSync`
- `Trigger: WebJobStatusSync`
- `Trigger: All Functions` (runs all sequentially)

### Option 2: Shell Script

```bash
# From workspace root
./.vscode/trigger-functions.sh [function-name]

# Examples:
./.vscode/trigger-functions.sh manifest
./.vscode/trigger-functions.sh webjob
./.vscode/trigger-functions.sh packageversion
./.vscode/trigger-functions.sh webjobstatus
./.vscode/trigger-functions.sh all
```

### Option 3: Direct curl

```bash
curl -X POST http://localhost:7071/api/ManifestSyncFunction_Http
curl -X POST http://localhost:7071/api/AppWebJobSyncFunction_Http
curl -X POST http://localhost:7071/api/PackageVersionSyncFunction_Http
curl -X POST http://localhost:7071/api/WebJobStatusSyncFunction_Http
```

## Debugging Tips

1. **Set breakpoints** in your function code
2. **Start debugging** with "Debug Azure Functions (Start & Attach)"
3. **Wait for PID display**, then select the correct `dotnet` process
4. **Trigger a function** using any of the methods above
5. **Breakpoint hits** and you can step through!

## Prerequisites

- Azure Functions must be running (`func: host start` task or F5)
- Functions listen on `http://localhost:7071` by default
- Azurite must be running for local storage emulation
