using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using CruSibyl.Core.Services;
using Microsoft.Azure.Functions.Worker.Http;
using System.Threading;
using Serilog;
using System;
using System.Net;

public class PackageVersionSyncFunction
{
    private static readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly IConfiguration _configuration;
    private readonly IPackageVersionSyncService _service;

    public PackageVersionSyncFunction(IConfiguration configuration, IPackageVersionSyncService service)
    {
        _configuration = configuration;
        _service = service;
    }

    [Function("PackageVersionSyncFunction")]
    public async Task Run([TimerTrigger("0 0 10 * * *")] TimerInfo timer)
    {
        await ExecuteSync("Timer trigger");
    }

    [Function("PackageVersionSyncFunction_Http")]
    public async Task<HttpResponseData> RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var wasExecuted = await ExecuteSync("Manual trigger");

        var response = req.CreateResponse(wasExecuted ? HttpStatusCode.OK : HttpStatusCode.Conflict);
        var message = wasExecuted
            ? "Package version sync triggered successfully."
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
            Log.Information("Starting package version sync from {TriggerSource} at {StartTime}",
                triggerSource, DateTime.UtcNow);

            await _service.SyncPackageVersionsAsync();

            Log.Information("Completed package version sync from {TriggerSource} at {EndTime}",
                triggerSource, DateTime.UtcNow);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Package version sync failed from {TriggerSource}", triggerSource);
            throw;
        }
        finally
        {
            _syncLock.Release();
        }
    }
}
