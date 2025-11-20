using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Models;
using CruSibyl.Core.Models.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace CruSibyl.Core.Services;

public interface IAzureSyncService
{
    /// <summary>
    /// Update all apps and their WebJobs in the database
    /// </summary>
    Task SyncAppsAndWebJobs(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sync WebJob status and run history for all WebJobs in the database
    /// </summary>
    Task SyncWebJobStatuses(string subscriptionId, CancellationToken cancellationToken = default);
}

public class AzureSyncService : IAzureSyncService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IAzureQueryService _azureQueryService;
    private readonly IEventService _eventService;
    private readonly AzureSettings _azureSettings;

    public AzureSyncService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IAzureQueryService azureQueryService,
        IEventService eventService,
        IOptions<AzureSettings> azureSettings)
    {
        _dbContextFactory = dbContextFactory;
        _azureQueryService = azureQueryService;
        _eventService = eventService;
        _azureSettings = azureSettings.Value;
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
                try
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
                List<WebJob> webJobs;
                try
                {
                    webJobs = await _azureQueryService.GetWebJobs(appToSync, cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to get WebJobs for app {AppName}, skipping WebJob sync for this app", appToSync.Name);
                    // Continue with next app instead of aborting entire sync
                    continue;
                }

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
                        
                        // Get detailed status (non-blocking if it fails)
                        try
                        {
                            var status = await _azureQueryService.GetWebJobStatus(existingWebJob, cancellationToken);
                            if (status != null)
                            {
                                existingWebJob.Status = status.Status;
                                existingWebJob.LastRunAt = status.LastRunAt;
                                existingWebJob.LastRunStatus = status.LastRunStatus;
                                existingWebJob.LastRunDurationMs = status.LastRunDurationMs;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to get detailed status for WebJob {WebJobName} in {AppName}, using basic info", 
                                existingWebJob.Name, appToSync.Name);
                            // Continue with basic WebJob info from the list
                        }
                    }
                }

                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to sync app {AppName}, continuing with next app", app.Name);
                    // Continue with next app instead of aborting entire sync
                }
            }

            Log.Information("Sync completed successfully for subscription {SubscriptionId}", subscriptionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to sync apps and WebJobs for subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    public async Task SyncWebJobStatuses(string subscriptionId, CancellationToken cancellationToken = default)
    {
        Log.Information("Starting WebJob status sync for subscription {SubscriptionId}", subscriptionId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        try
        {
            var now = DateTime.UtcNow;

            // Get all WebJobs for this subscription
            var webJobs = await dbContext.WebJobs
                .Include(wj => wj.App)
                .Where(wj => wj.App.SubscriptionId == subscriptionId && wj.JobType == "Triggered")
                .ToListAsync(cancellationToken);

            Log.Information("Found {Count} triggered WebJobs to sync with max concurrency {MaxConcurrency}", 
                webJobs.Count, _azureSettings.MaxConcurrentStatusQueries);

            // Use semaphore to throttle concurrent API calls
            using var semaphore = new SemaphoreSlim(_azureSettings.MaxConcurrentStatusQueries);

            var tasks = webJobs.Select(async webJob =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await ProcessWebJobStatus(webJob, now, dbContext, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            await dbContext.SaveChangesAsync(cancellationToken);

            Log.Information("WebJob status sync completed successfully for subscription {SubscriptionId}", subscriptionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to sync WebJob statuses for subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    private async Task ProcessWebJobStatus(WebJob webJob, DateTime now, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            // Get the full run history
            var runDetails = await _azureQueryService.GetWebJobRunHistory(webJob, cancellationToken);

            if (runDetails?.LatestRuns == null || runDetails.LatestRuns.Count == 0)
            {
                Log.Warning("No run history found for WebJob {WebJobName} in {AppName}", webJob.Name, webJob.App.Name);
                return;
            }

            // Determine which runs to create events for
            List<KuduLatestRun> runsToLog;
            bool isFirstCheck = webJob.LastStatusCheckAt == null;

            if (isFirstCheck)
            {
                // First check: Only log the latest run
                runsToLog = new List<KuduLatestRun> { runDetails.LatestRuns[0] };
                Log.Information("First status check for WebJob {WebJobName}, logging latest run only", webJob.Name);
            }
            else
            {
                // Subsequent checks: Log all runs since last check (with 1 minute buffer for clock skew)
                var checkThreshold = webJob.LastStatusCheckAt!.Value.AddMinutes(-1);
                runsToLog = runDetails.LatestRuns
                    .Where(r => r.StartTime > checkThreshold)
                    .ToList();
                
                Log.Information("Found {NewRunCount} new runs for WebJob {WebJobName} since {LastCheck}", 
                    runsToLog.Count, webJob.Name, webJob.LastStatusCheckAt);
            }

            // Create events for each run
            foreach (var run in runsToLog)
            {
                // Fetch log output if available
                string? logOutput = null;
                bool logTruncated = false;

                if (!string.IsNullOrEmpty(run.OutputUrl))
                {
                    logOutput = await _azureQueryService.GetWebJobRunOutput(run.OutputUrl, cancellationToken);
                    logTruncated = logOutput?.Contains("[... Output truncated ...]") == true;
                }

                // Calculate duration
                long? durationMs = run.GetDurationMs();
                if (!durationMs.HasValue && run.EndTime.HasValue && run.StartTime.HasValue)
                {
                    durationMs = (long)(run.EndTime.Value - run.StartTime.Value).TotalMilliseconds;
                }

                // Create payload
                var payload = new WebJobStatusPayload
                {
                    RunId = run.Id,
                    StartTime = run.StartTime,
                    EndTime = run.EndTime,
                    DurationMs = durationMs,
                    Status = run.Status,
                    LogOutput = logOutput,
                    LogTruncated = logTruncated,
                    OutputUrl = run.OutputUrl,
                    ErrorUrl = run.ErrorUrl
                };

                // Determine severity based on status
                string severity = run.Status?.ToLower() switch
                {
                    "success" => "Info",
                    "failed" => "Error",
                    _ => "Warning"
                };

                // Log event
                await _eventService.LogEvent(
                    EventType.WebJobStatus,
                    $"WebJob {webJob.Name} run {run.Status ?? "Unknown"}",
                    severity: severity,
                    appIds: [webJob.AppId],
                    webJobIds: [webJob.Id],
                    details: $"Duration: {durationMs}ms, Started: {run.StartTime:yyyy-MM-dd HH:mm:ss} UTC",
                    source: "AzureSyncService",
                    payload: payload,
                    cancellationToken: cancellationToken);

                Log.Debug("Logged status event for WebJob {WebJobName} run {RunId} with status {Status}", 
                    webJob.Name, run.Id, run.Status);
            }

            // Update WebJob status and last check time
            var latestRun = runDetails.LatestRuns[0];
            webJob.Status = latestRun.Status;
            webJob.LastRunAt = latestRun.StartTime;
            webJob.LastRunStatus = latestRun.Status;
            webJob.LastRunDurationMs = latestRun.GetDurationMs() ?? 
                (latestRun.EndTime.HasValue && latestRun.StartTime.HasValue 
                    ? (long)(latestRun.EndTime.Value - latestRun.StartTime.Value).TotalMilliseconds 
                    : null);
            webJob.LastStatusCheckAt = now;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to sync status for WebJob {WebJobName} in {AppName}", webJob.Name, webJob.App.Name);
            // Continue processing other WebJobs
        }
    }
}
