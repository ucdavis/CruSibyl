using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace CruSibyl.Core.Services;

public interface IAzureSyncService
{
    /// <summary>
    /// Update all apps and their WebJobs in the database
    /// </summary>
    Task SyncAppsAndWebJobs(string subscriptionId, CancellationToken cancellationToken = default);
}

public class AzureSyncService : IAzureSyncService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IAzureQueryService _azureQueryService;
    private readonly IEventService _eventService;

    public AzureSyncService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IAzureQueryService azureQueryService,
        IEventService eventService)
    {
        _dbContextFactory = dbContextFactory;
        _azureQueryService = azureQueryService;
        _eventService = eventService;
    }

    public async Task SyncAppsAndWebJobs(string subscriptionId, CancellationToken cancellationToken = default)
    {
        Log.Information("Starting sync for subscription {SubscriptionId}", subscriptionId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        try
        {
            // Get apps
            var apps = await _azureQueryService.GetApps(subscriptionId, cancellationToken);

            foreach (var app in apps)
            {
                // Check if app exists in database
                var existingApp = await dbContext.Apps
                    .Include(a => a.WebJobs)
                    .FirstOrDefaultAsync(a => a.Name == app.Name && 
                                            a.SubscriptionId == subscriptionId, 
                                            cancellationToken);

                App appToSync;
                if (existingApp == null)
                {
                    // New app
                    dbContext.Apps.Add(app);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    
                    // Log discovery event
                    await _eventService.LogEvent(
                        EventType.AppDiscovery,
                        $"Discovered new App Service: {app.Name}",
                        severity: "Info",
                        appIds: [app.Id],
                        details: $"Subscription: {subscriptionId}, ResourceGroup: {app.ResourceGroup}, Kind: {app.Kind}",
                        source: "AzureSyncService",
                        cancellationToken: cancellationToken);
                    
                    appToSync = app;
                }
                else
                {
                    // Update existing app
                    existingApp.ResourceGroup = app.ResourceGroup;
                    existingApp.ResourceId = app.ResourceId;
                    existingApp.DefaultHostName = app.DefaultHostName;
                    existingApp.Sku = app.Sku;
                    existingApp.RuntimeStack = app.RuntimeStack;
                    existingApp.Kind = app.Kind;
                    existingApp.State = app.State;
                    existingApp.CreatedAt = app.CreatedAt;
                    existingApp.IsEnabled = app.IsEnabled;

                    appToSync = existingApp;
                }

                // Get and sync WebJobs
                var webJobs = await _azureQueryService.GetWebJobs(appToSync, cancellationToken);

                foreach (var webJob in webJobs)
                {
                    var existingWebJob = appToSync.WebJobs.FirstOrDefault(wj => wj.Name == webJob.Name);

                    if (existingWebJob == null)
                    {
                        // Ensure the AppId is set correctly
                        webJob.AppId = appToSync.Id;
                        dbContext.WebJobs.Add(webJob);
                        
                        // Save to get the WebJob ID
                        await dbContext.SaveChangesAsync(cancellationToken);
                        
                        // Log discovery event
                        await _eventService.LogEvent(
                            EventType.WebJobDiscovery,
                            $"Discovered new WebJob: {webJob.Name} in {appToSync.Name}",
                            severity: "Info",
                            appIds: [appToSync.Id],
                            webJobIds: [webJob.Id],
                            details: $"JobType: {webJob.JobType}, Status: {webJob.Status}",
                            source: "AzureSyncService",
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        // Update existing WebJob
                        existingWebJob.JobType = webJob.JobType;
                        existingWebJob.Status = webJob.Status;
                        existingWebJob.RunMode = webJob.RunMode;
                        existingWebJob.Url = webJob.Url;
                        existingWebJob.ExtraInfoUrl = webJob.ExtraInfoUrl;
                        existingWebJob.IsEnabled = webJob.IsEnabled;

                        // Ensure App navigation property is loaded for status query
                        existingWebJob.App = appToSync;
                        
                        // Get detailed status
                        var status = await _azureQueryService.GetWebJobStatus(existingWebJob, cancellationToken);
                        if (status != null)
                        {
                            existingWebJob.Status = status.Status;
                            existingWebJob.LastRunAt = status.LastRunAt;
                            existingWebJob.LastRunStatus = status.LastRunStatus;
                            existingWebJob.LastRunDurationMs = status.LastRunDurationMs;
                        }
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            Log.Information("Sync completed successfully for subscription {SubscriptionId}", subscriptionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to sync apps and WebJobs for subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }
}
