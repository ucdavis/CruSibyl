using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using CruSibyl.Core.Services;
using CruSibyl.Core.Models.Settings;
using Microsoft.Azure.Functions.Worker.Http;
using Serilog;
using System;
using System.Linq;
using System.Net;

public class AppWebJobSyncFunction : SyncFunctionBase
{
    private readonly AzureSettings _azureSettings;
    private readonly IAzureSyncService _syncService;

    public AppWebJobSyncFunction(IOptions<AzureSettings> azureSettings, IAzureSyncService syncService)
    {
        _azureSettings = azureSettings.Value;
        _syncService = syncService;
    }

    [Function("AppWebJobSyncFunction")]
    public async Task Run([TimerTrigger("0 0 2 * * *")] TimerInfo timer)
    {
        await ExecuteSync(async () =>
        {
            var enabledSubscriptions = _azureSettings.Subscriptions
                .Where(kvp => kvp.Value.Enabled && !string.IsNullOrEmpty(kvp.Value.SubscriptionId))
                .ToList();
            
            if (!enabledSubscriptions.Any())
            {
                Log.Warning("No enabled Azure subscriptions configured in appsettings.json");
                return;
            }

            Log.Information("Starting App and WebJob sync for {Count} subscription(s)", enabledSubscriptions.Count);

            foreach (var (name, subscription) in enabledSubscriptions)
            {
                try
                {
                    Log.Information("Syncing subscription: {Name} ({SubscriptionId})", 
                        name, subscription.SubscriptionId);
                    
                    await _syncService.SyncAppsAndWebJobs(subscription.SubscriptionId);
                    
                    Log.Information("Completed sync for subscription: {Name}", name);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to sync subscription: {Name} ({SubscriptionId})", 
                        name, subscription.SubscriptionId);
                    // Continue with other subscriptions even if one fails
                }
            }

            Log.Information("Completed App and WebJob sync for all subscriptions");
        }, "Timer trigger (2 AM daily)", "App/WebJob");
    }

    [Function("AppWebJobSyncFunction_Http")]
    public async Task<HttpResponseData> RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var wasExecuted = await ExecuteSync(async () =>
        {
            var enabledSubscriptions = _azureSettings.Subscriptions
                .Where(kvp => kvp.Value.Enabled && !string.IsNullOrEmpty(kvp.Value.SubscriptionId))
                .ToList();
            
            if (!enabledSubscriptions.Any())
            {
                Log.Warning("No enabled Azure subscriptions configured in appsettings.json");
                return;
            }

            Log.Information("Starting App and WebJob sync for {Count} subscription(s)", enabledSubscriptions.Count);

            foreach (var (name, subscription) in enabledSubscriptions)
            {
                try
                {
                    Log.Information("Syncing subscription: {Name} ({SubscriptionId})", 
                        name, subscription.SubscriptionId);
                    
                    await _syncService.SyncAppsAndWebJobs(subscription.SubscriptionId);
                    
                    Log.Information("Completed sync for subscription: {Name}", name);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to sync subscription: {Name} ({SubscriptionId})", 
                        name, subscription.SubscriptionId);
                    // Continue with other subscriptions even if one fails
                }
            }

            Log.Information("Completed App and WebJob sync for all subscriptions");
        }, "Manual HTTP trigger", "App/WebJob");

        var response = req.CreateResponse(wasExecuted ? HttpStatusCode.OK : HttpStatusCode.Conflict);
        var message = wasExecuted
            ? "App and WebJob sync triggered successfully for all enabled subscriptions."
            : "Sync is already running. Please wait for completion.";

        await response.WriteStringAsync(message);
        return response;
    }
}
