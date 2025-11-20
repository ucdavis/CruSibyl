using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CruSibyl.Core.Models.Settings;
using CruSibyl.Core.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using Serilog;

namespace CruSibyl.Functions;

public class WebJobStatusSyncFunction
{
    private readonly IAzureSyncService _azureSyncService;
    private readonly AzureSettings _azureSettings;

    public WebJobStatusSyncFunction(
        IAzureSyncService azureSyncService,
        IOptions<AzureSettings> azureSettings)
    {
        _azureSyncService = azureSyncService;
        _azureSettings = azureSettings.Value;
    }

    [Function("WebJobStatusSyncFunction_Timer")]
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
        var enabledSubscriptions = _azureSettings.Subscriptions
            .Where(kvp => kvp.Value.Enabled && !string.IsNullOrEmpty(kvp.Value.SubscriptionId))
            .ToList();

        if (!enabledSubscriptions.Any())
        {
            Log.Warning("No enabled Azure subscriptions configured");
            return;
        }

        Log.Information("Starting WebJob status sync for {Count} subscription(s)", enabledSubscriptions.Count);

        foreach (var (subscriptionName, subscription) in enabledSubscriptions)
        {
            Log.Information("Syncing WebJob statuses for subscription: {SubscriptionName} ({SubscriptionId})", 
                subscriptionName, subscription.SubscriptionId);

            try
            {
                await _azureSyncService.SyncWebJobStatuses(subscription.SubscriptionId, cancellationToken);
                Log.Information("Successfully synced WebJob statuses for subscription: {SubscriptionName}", subscriptionName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to sync WebJob statuses for subscription: {SubscriptionName}", subscriptionName);
                // Continue with next subscription
            }
        }

        Log.Information("WebJob status sync completed");
    }
}
