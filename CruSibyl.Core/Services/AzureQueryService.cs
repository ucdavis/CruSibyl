using Azure.Identity;
using Azure.Monitor.Query;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using CruSibyl.Core.Domain;
using Serilog;

namespace CruSibyl.Core.Services;

public interface IAzureQueryService
{
    /// <summary>
    /// Get all App Services in the specified subscription
    /// </summary>
    Task<List<App>> GetApps(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get WebJobs for a specific App Service
    /// </summary>
    Task<List<WebJob>> GetWebJobs(App app, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get WebJob status and run history
    /// </summary>
    Task<WebJobStatus?> GetWebJobStatus(WebJob webJob, CancellationToken cancellationToken = default);
}

public class WebJobStatus
{
    public string Status { get; set; } = null!;
    public DateTime? LastRunAt { get; set; }
    public string? LastRunStatus { get; set; }
    public long? LastRunDurationMs { get; set; }
}

public class AzureQueryService : IAzureQueryService
{
    private readonly ArmClient _armClient;
    private readonly LogsQueryClient? _logsQueryClient;
    private readonly IKuduApiService _kuduApiService;

    public AzureQueryService(
        IKuduApiService kuduApiService,
        DefaultAzureCredential? credential = null)
    {
        _kuduApiService = kuduApiService;
        
        // Use provided credential or create a new one
        var azureCredential = credential ?? new DefaultAzureCredential();
        _armClient = new ArmClient(azureCredential);
        
        // Initialize logs query client for monitoring queries
        try
        {
            _logsQueryClient = new LogsQueryClient(azureCredential);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize LogsQueryClient. Log querying will not be available.");
        }
    }

    public async Task<List<App>> GetApps(string subscriptionId, CancellationToken cancellationToken = default)
    {
        Log.Information("Getting App Services in subscription {SubscriptionId}", subscriptionId);

        var apps = new List<App>();
        var subscription = _armClient.GetSubscriptionResource(new Azure.Core.ResourceIdentifier($"/subscriptions/{subscriptionId}"));

        try
        {
            await foreach (var webSite in subscription.GetWebSitesAsync(cancellationToken))
            {
                var data = webSite.Data;
                var app = new App
                {
                    Name = data.Name,
                    ResourceGroup = webSite.Id.ResourceGroupName ?? "Unknown",
                    SubscriptionId = subscriptionId,
                    ResourceId = data.Id.ToString(),
                    DefaultHostName = data.DefaultHostName,
                    Sku = data.AppServicePlanId?.ToString().Split('/').LastOrDefault(),
                    Kind = data.Kind,
                    State = data.State,
                    CreatedAt = DateTime.UtcNow,
                    IsEnabled = data.IsEnabled ?? true
                };

                // Try to extract runtime stack
                if (data.SiteConfig?.LinuxFxVersion != null)
                {
                    app.RuntimeStack = data.SiteConfig.LinuxFxVersion;
                }
                else if (data.SiteConfig?.NetFrameworkVersion != null)
                {
                    app.RuntimeStack = $".NET {data.SiteConfig.NetFrameworkVersion}";
                }

                apps.Add(app);
            }

            Log.Information("Found {Count} App Services", apps.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get App Services in subscription {SubscriptionId}", subscriptionId);
            throw;
        }

        return apps;
    }

    public async Task<List<WebJob>> GetWebJobs(App app, CancellationToken cancellationToken = default)
    {
        Log.Information("Getting WebJobs for app {AppName}", app.Name);

        var webJobs = new List<WebJob>();

        if (string.IsNullOrEmpty(app.Name))
        {
            Log.Warning("App has no Name, cannot get WebJobs");
            return webJobs;
        }

        try
        {
            // Extract slot name if present (format: sitename/slots/slotname)
            string siteName = app.Name;
            string? slotName = null;
            
            if (app.ResourceId?.Contains("/slots/") == true)
            {
                var parts = app.ResourceId.Split(new[] { "/slots/" }, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    slotName = parts[1].Split('/')[0];
                }
            }

            // Get continuous WebJobs using Kudu API
            var continuousWebJobs = await _kuduApiService.GetContinuousWebJobs(siteName, slotName, cancellationToken);
            foreach (var kuduJob in continuousWebJobs)
            {
                var webJob = new WebJob
                {
                    AppId = app.Id,
                    Name = kuduJob.Name ?? "Unknown",
                    JobType = "Continuous",
                    Status = kuduJob.Status,
                    Url = kuduJob.Url,
                    ExtraInfoUrl = kuduJob.ExtraInfoUrl,
                    CreatedAt = DateTime.UtcNow,
                    IsEnabled = true
                };

                webJobs.Add(webJob);
            }

            // Get triggered WebJobs using Kudu API
            var triggeredWebJobs = await _kuduApiService.GetTriggeredWebJobs(siteName, slotName, cancellationToken);
            foreach (var kuduJob in triggeredWebJobs)
            {
                var webJob = new WebJob
                {
                    AppId = app.Id,
                    Name = kuduJob.Name ?? "Unknown",
                    JobType = "Triggered",
                    RunMode = kuduJob.RunCommand != null ? "OnDemand" : "Scheduled",
                    Url = kuduJob.Url,
                    ExtraInfoUrl = kuduJob.ExtraInfoUrl,
                    CreatedAt = DateTime.UtcNow,
                    IsEnabled = true
                };

                // Populate latest run information
                if (kuduJob.LatestRun != null)
                {
                    webJob.LastRunAt = kuduJob.LatestRun.StartTime;
                    webJob.LastRunStatus = kuduJob.LatestRun.Status;
                    if (kuduJob.LatestRun.Duration.HasValue)
                    {
                        webJob.LastRunDurationMs = kuduJob.LatestRun.Duration.Value;
                    }
                    else if (kuduJob.LatestRun.EndTime.HasValue && kuduJob.LatestRun.StartTime.HasValue)
                    {
                        webJob.LastRunDurationMs = (long)(kuduJob.LatestRun.EndTime.Value - kuduJob.LatestRun.StartTime.Value).TotalMilliseconds;
                    }
                }

                webJobs.Add(webJob);
            }

            Log.Information("Found {Count} WebJobs for app {AppName}", webJobs.Count, app.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get WebJobs for app {AppName}", app.Name);
            throw;
        }

        return webJobs;
    }

    public async Task<WebJobStatus?> GetWebJobStatus(WebJob webJob, CancellationToken cancellationToken = default)
    {
        Log.Debug("Getting status for WebJob {WebJobName} in app {AppName}", webJob.Name, webJob.App.Name);

        if (string.IsNullOrEmpty(webJob.App.Name) || string.IsNullOrEmpty(webJob.Name))
        {
            Log.Warning("App or WebJob name is missing, cannot get WebJob status");
            return null;
        }

        try
        {
            // Extract slot name if present
            string siteName = webJob.App.Name;
            string? slotName = null;

            if (webJob.App.ResourceId?.Contains("/slots/") == true)
            {
                var parts = webJob.App.ResourceId.Split(new[] { "/slots/" }, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    slotName = parts[1].Split('/')[0];
                }
            }

            if (webJob.JobType == "Continuous")
            {
                // For continuous jobs, get the current status
                var continuousJobs = await _kuduApiService.GetContinuousWebJobs(siteName, slotName, cancellationToken);
                var job = continuousJobs.FirstOrDefault(j => j.Name == webJob.Name);
                
                if (job != null)
                {
                    return new WebJobStatus
                    {
                        Status = job.Status ?? "Unknown",
                        LastRunAt = null, // Continuous jobs don't have discrete runs
                        LastRunStatus = job.Status,
                        LastRunDurationMs = null
                    };
                }
            }
            else if (webJob.JobType == "Triggered")
            {
                // For triggered jobs, get detailed run history
                var details = await _kuduApiService.GetTriggeredWebJobDetails(siteName, webJob.Name, slotName, cancellationToken);
                
                if (details?.LatestRuns != null && details.LatestRuns.Count > 0)
                {
                    var latestRun = details.LatestRuns[0];
                    long? durationMs = latestRun.Duration;
                    
                    if (!durationMs.HasValue && latestRun.EndTime.HasValue && latestRun.StartTime.HasValue)
                    {
                        durationMs = (long)(latestRun.EndTime.Value - latestRun.StartTime.Value).TotalMilliseconds;
                    }

                    return new WebJobStatus
                    {
                        Status = latestRun.Status ?? "Unknown",
                        LastRunAt = latestRun.StartTime,
                        LastRunStatus = latestRun.Status,
                        LastRunDurationMs = durationMs
                    };
                }
            }

            Log.Warning("No status information found for WebJob {WebJobName} in app {AppName}", webJob.Name, webJob.App.Name);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get status for WebJob {WebJobName} in app {AppName}", webJob.Name, webJob.App.Name);
            throw;
        }
    }
}
