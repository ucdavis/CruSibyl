using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using CruSibyl.Core.Services;
using Microsoft.Azure.Functions.Worker.Http;
using System.Threading;
using Serilog;
using System;
using System.Net;

public class ManifestSyncFunction
{
    private static readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly IConfiguration _configuration;
    private readonly IManifestSyncService _service;

    public ManifestSyncFunction(IConfiguration configuration, IManifestSyncService service)
    {
        _configuration = configuration;
        _service = service;
    }

    [Function("ManifestSyncFunction")]
    public async Task Run([TimerTrigger("0 0 8 * * *")] TimerInfo timer)
    {
        await ExecuteSync("Timer trigger");
    }

    [Function("ManifestSyncFunction_Http")]
    public async Task<HttpResponseData> RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var wasExecuted = await ExecuteSync("Manual trigger");

        var response = req.CreateResponse(wasExecuted ? HttpStatusCode.OK : HttpStatusCode.Conflict);
        var message = wasExecuted
            ? "Manifest sync triggered successfully."
            : "Sync is already running. Please wait for completion.";

        await response.WriteStringAsync(message);
        return response;
    }

    private async Task<bool> ExecuteSync(string triggerSource)
    {
        // Try to acquire the lock with a very short timeout
        if (!await _syncLock.WaitAsync(TimeSpan.FromMilliseconds(100)))
        {
            Log.Warning("Sync already in progress, skipping execution from {TriggerSource}", triggerSource);
            return false;
        }

        try
        {
            Log.Information("Starting manifest sync from {TriggerSource} at {StartTime}",
                triggerSource, DateTime.UtcNow);

            await _service.SyncManifestsAsync();

            Log.Information("Completed manifest sync from {TriggerSource} at {EndTime}",
                triggerSource, DateTime.UtcNow);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Manifest sync failed from {TriggerSource}", triggerSource);
            throw;
        }
        finally
        {
            _syncLock.Release();
        }
    }
}
