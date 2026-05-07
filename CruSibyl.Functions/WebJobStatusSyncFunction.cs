using System;
using System.Threading;
using System.Threading.Tasks;
using CruSibyl.Core.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Serilog;

namespace CruSibyl.Functions;

public class WebJobStatusSyncFunction
{
    private readonly IAzureSyncService _azureSyncService;

    public WebJobStatusSyncFunction(IAzureSyncService azureSyncService)
    {
        _azureSyncService = azureSyncService;
    }

    [Function("WebJobStatusSyncFunction")]
    public async Task RunTimer(
        [TimerTrigger("0 */15 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        Log.Information("WebJob status sync timer trigger executed at: {Time}", DateTime.UtcNow);
        await SyncWebJobStatuses(cancellationToken);
    }

    [Function("WebJobStatusSyncFunction_Http")]
    public async Task RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        Log.Information("WebJob status sync HTTP trigger executed at: {Time}", DateTime.UtcNow);
        await SyncWebJobStatuses(cancellationToken);
    }

    private async Task SyncWebJobStatuses(CancellationToken cancellationToken)
    {
        Log.Information("Starting WebJob status sync for configured subscription");
        await _azureSyncService.SyncWebJobStatuses(cancellationToken);
        Log.Information("WebJob status sync completed for configured subscription");
    }
}
