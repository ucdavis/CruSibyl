using Microsoft.Azure.Functions.Worker;
using System.Threading.Tasks;
using CruSibyl.Core.Services;
using Microsoft.Azure.Functions.Worker.Http;
using Serilog;
using System.Net;

public class AppWebJobSyncFunction : SyncFunctionBase
{
    private readonly IAzureSyncService _syncService;

    public AppWebJobSyncFunction(IAzureSyncService syncService)
    {
        _syncService = syncService;
    }

    [Function("AppWebJobSyncFunction")]
    public async Task Run([TimerTrigger("0 0 2 * * *")] TimerInfo timer)
    {
        await ExecuteSync(async () =>
        {
            Log.Information("Starting App and WebJob sync for configured subscription");
            await _syncService.SyncAppsAndWebJobs();
            Log.Information("Completed App and WebJob sync for configured subscription");
        }, "Timer trigger (2 AM daily)", "App/WebJob");
    }

    [Function("AppWebJobSyncFunction_Http")]
    public async Task<HttpResponseData> RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var wasExecuted = await ExecuteSync(async () =>
        {
            Log.Information("Starting App and WebJob sync for configured subscription");
            await _syncService.SyncAppsAndWebJobs();
            Log.Information("Completed App and WebJob sync for configured subscription");
        }, "Manual HTTP trigger", "App/WebJob");

        var response = req.CreateResponse(wasExecuted ? HttpStatusCode.OK : HttpStatusCode.Conflict);
        var message = wasExecuted
            ? "App and WebJob sync triggered successfully for the configured subscription."
            : "Sync is already running. Please wait for completion.";

        await response.WriteStringAsync(message);
        return response;
    }
}
