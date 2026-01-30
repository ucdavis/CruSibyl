using System.Text.Json;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Models;
using CruSibyl.Web.Models.Dashboard;
using CruSibyl.Web.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CruSibyl.Web.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardOverviewAsync(string subscriptionId);
    Task<WebJobFailureHistoryViewModel> GetFailureHistoryAsync(string subscriptionId, string? appFilter = null, string? jobFilter = null);
    Task<WebJobDrillDownViewModel> GetWebJobDrillDownAsync(int webJobId);
    Task<DependencyCurrencyViewModel> GetDependencyCurrencyAsync(string subscriptionId);
    Dictionary<string, AzureSubscription> GetSubscriptions();
    string GetDefaultSubscriptionId();
}

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _dbContext;
    private readonly Dictionary<string, AzureSubscription> _subscriptions;

    public DashboardService(AppDbContext dbContext, IOptions<AzureConfig> azureConfig)
    {
        _dbContext = dbContext;
        _subscriptions = azureConfig.Value.Subscriptions.Where(s => s.Value.Enabled).ToDictionary(s => s.Key, s => s.Value);
    }

    public Dictionary<string, AzureSubscription> GetSubscriptions() => _subscriptions;

    public string GetDefaultSubscriptionId()
    {
        var defaultSub = _subscriptions.FirstOrDefault(s => s.Value.Default);
        return defaultSub.Value?.SubscriptionId ?? _subscriptions.FirstOrDefault().Value?.SubscriptionId ?? string.Empty;
    }

    public async Task<DashboardViewModel> GetDashboardOverviewAsync(string subscriptionId)
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        
        // Filter apps by subscription
        var subscriptionName = _subscriptions.FirstOrDefault(s => s.Value.SubscriptionId == subscriptionId).Key ?? subscriptionId;
        var apps = await _dbContext.Apps
            .Include(a => a.WebJobs)
            .Where(a => a.IsEnabled)
            .Where(a => a.SubscriptionId == subscriptionId && a.WebJobs.Any(wj => wj.IsEnabled))
            .ToListAsync();

        var appIds = apps.Select(a => a.Id).ToList();
        var webJobIds = apps.SelectMany(a => a.WebJobs).Select(wj => wj.Id).ToList();

        // Get recent failure events for these apps
        var recentFailures = await _dbContext.Events
            .Where(e => e.EventType == EventType.WebJobStatus)
            .Where(e => e.Timestamp >= thirtyDaysAgo)
            .Where(e => e.Severity == "Error" || e.Severity == "Critical")
            .Where(e => e.WebJobs.Any(wj => webJobIds.Contains(wj.Id)))
            .Include(e => e.WebJobs)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync();

        var appHealthCards = new List<AppHealthCardModel>();
        var criticalAlerts = new List<CriticalAlert>();

        foreach (var app in apps)
        {
            var appWebJobs = app.WebJobs.Where(wj => wj.IsEnabled).ToList();
            var appWebJobIds = appWebJobs.Select(wj => wj.Id).ToHashSet();
            var appFailures = recentFailures.Where(e => e.WebJobs.Any(wj => appWebJobIds.Contains(wj.Id))).ToList();

            var lastFailure = appFailures.FirstOrDefault();
            var lastFailureWebJob = lastFailure?.WebJobs.FirstOrDefault(wj => appWebJobIds.Contains(wj.Id));

            // Check for critical failures (3+ failures in last 24 hours)
            var last24Hours = DateTime.UtcNow.AddHours(-24);
            var recentFailureCount = appFailures.Count(e => e.Timestamp >= last24Hours);
            var hasCriticalFailure = recentFailureCount >= 3;

            if (hasCriticalFailure && lastFailure != null && lastFailureWebJob != null)
            {
                var payload = lastFailure.Payload.HasValue 
                    ? JsonSerializer.Deserialize<WebJobStatusPayload>(lastFailure.Payload.Value.GetRawText())
                    : null;

                // Check if failure is consistent (>80% failure rate in last 7 days)
                var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                var recentWeekFailures = appFailures.Count(e => e.Timestamp >= sevenDaysAgo && e.WebJobs.Any(wj => wj.Id == lastFailureWebJob.Id));
                var isConsistent = recentWeekFailures > 5; // Simple heuristic

                criticalAlerts.Add(new CriticalAlert
                {
                    AppId = app.Id,
                    AppName = app.Name,
                    WebJobId = lastFailureWebJob.Id,
                    WebJobName = lastFailureWebJob.Name,
                    FailureTime = lastFailure.Timestamp,
                    ErrorMessage = payload?.ErrorOutput ?? lastFailure.Message,
                    IsConsistent = isConsistent
                });
            }

            appHealthCards.Add(new AppHealthCardModel
            {
                AppId = app.Id,
                AppName = app.Name,
                ResourceGroup = app.ResourceGroup,
                SubscriptionId = app.SubscriptionId,
                Importance = app.Importance,
                TotalWebJobs = appWebJobs.Count,
                RunningWebJobs = appWebJobs.Count(wj => wj.Status == "Running"),
                FailedWebJobs = appWebJobs.Count(wj => wj.LastRunStatus == "Failed"),
                LastFailureAt = lastFailure?.Timestamp,
                LastFailureJobName = lastFailureWebJob?.Name,
                HasCriticalFailure = hasCriticalFailure
            });
        }

        return new DashboardViewModel
        {
            SubscriptionId = subscriptionId,
            SubscriptionName = subscriptionName,
            Subscriptions = _subscriptions,
            AppHealthCards = appHealthCards.OrderByDescending(a => a.HasCriticalFailure).ThenByDescending(a => a.Importance).ToList(),
            CriticalAlerts = criticalAlerts.OrderByDescending(a => a.FailureTime).ToList()
        };
    }

    public async Task<WebJobFailureHistoryViewModel> GetFailureHistoryAsync(string subscriptionId, string? appFilter = null, string? jobFilter = null)
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var subscriptionName = _subscriptions.FirstOrDefault(s => s.Value.SubscriptionId == subscriptionId).Key ?? subscriptionId;

        var query = _dbContext.Events
            .Where(e => e.EventType == EventType.WebJobStatus)
            .Where(e => e.Timestamp >= thirtyDaysAgo)
            .Where(e => e.Severity == "Error" || e.Severity == "Critical")
            .Include(e => e.WebJobs)
                .ThenInclude(wj => wj.App)
            .AsQueryable();

        // Filter by subscription
        query = query.Where(e => e.WebJobs.Any(wj => wj.App.SubscriptionId == subscriptionId));

        if (!string.IsNullOrWhiteSpace(appFilter))
        {
            query = query.Where(e => e.WebJobs.Any(wj => wj.App.Name.Contains(appFilter)));
        }

        if (!string.IsNullOrWhiteSpace(jobFilter))
        {
            query = query.Where(e => e.WebJobs.Any(wj => wj.Name.Contains(jobFilter)));
        }

        var events = await query.OrderByDescending(e => e.Timestamp).Take(100).ToListAsync();

        var failures = new List<WebJobFailureHistoryModel>();
        var webJobFailureCounts = new Dictionary<int, List<int>>();

        foreach (var evt in events)
        {
            var webJob = evt.WebJobs.FirstOrDefault();
            if (webJob == null) continue;

            var payload = evt.Payload.HasValue 
                ? JsonSerializer.Deserialize<WebJobStatusPayload>(evt.Payload.Value.GetRawText())
                : null;

            // Calculate if this is a consistent failure
            var recentFailures = events.Count(e => 
                e.WebJobs.Any(wj => wj.Id == webJob.Id) && 
                e.Timestamp >= evt.Timestamp.AddDays(-7));
            var isConsistent = recentFailures > 5;

            failures.Add(new WebJobFailureHistoryModel
            {
                WebJobId = webJob.Id,
                WebJobName = webJob.Name,
                AppId = webJob.App.Id,
                AppName = webJob.App.Name,
                SubscriptionId = subscriptionId,
                SubscriptionName = subscriptionName,
                FailureTime = evt.Timestamp,
                Status = payload?.Status ?? "Failed",
                DurationMs = payload?.DurationMs,
                ErrorOutput = payload?.ErrorOutput,
                IsConsistent = isConsistent
            });

            // Build sparkline data (30 daily buckets)
            if (!webJobFailureCounts.ContainsKey(webJob.Id))
            {
                var dailyCounts = new List<int>();
                for (int i = 0; i < 30; i++)
                {
                    var dayStart = thirtyDaysAgo.AddDays(i);
                    var dayEnd = dayStart.AddDays(1);
                    var count = events.Count(e => 
                        e.WebJobs.Any(wj => wj.Id == webJob.Id) && 
                        e.Timestamp >= dayStart && 
                        e.Timestamp < dayEnd);
                    dailyCounts.Add(count);
                }
                webJobFailureCounts[webJob.Id] = dailyCounts;
            }
        }

        return new WebJobFailureHistoryViewModel
        {
            SubscriptionId = subscriptionId,
            SubscriptionName = subscriptionName,
            AppFilter = appFilter,
            JobFilter = jobFilter,
            Failures = failures,
            FailureSparklines = webJobFailureCounts
        };
    }

    public async Task<WebJobDrillDownViewModel> GetWebJobDrillDownAsync(int webJobId)
    {
        var webJob = await _dbContext.WebJobs
            .Include(wj => wj.App)
            .FirstOrDefaultAsync(wj => wj.Id == webJobId);

        if (webJob == null)
        {
            throw new ArgumentException($"WebJob with ID {webJobId} not found");
        }

        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var events = await _dbContext.Events
            .Where(e => e.EventType == EventType.WebJobStatus)
            .Where(e => e.WebJobs.Any(wj => wj.Id == webJobId))
            .Where(e => e.Timestamp >= thirtyDaysAgo)
            .OrderByDescending(e => e.Timestamp)
            .Take(50)
            .ToListAsync();

        var runs = new List<WebJobRunModel>();
        foreach (var evt in events)
        {
            var payload = evt.Payload.HasValue 
                ? JsonSerializer.Deserialize<WebJobStatusPayload>(evt.Payload.Value.GetRawText())
                : null;

            if (payload == null) continue;

            // Truncate log preview to 500 chars
            var logPreview = payload.LogOutput?.Length > 500 
                ? payload.LogOutput.Substring(0, 500) + "..." 
                : payload.LogOutput;
            var errorPreview = payload.ErrorOutput?.Length > 500 
                ? payload.ErrorOutput.Substring(0, 500) + "..." 
                : payload.ErrorOutput;

            runs.Add(new WebJobRunModel
            {
                RunId = payload.RunId,
                StartTime = payload.StartTime,
                EndTime = payload.EndTime,
                DurationMs = payload.DurationMs,
                Status = payload.Status,
                LogOutputPreview = logPreview,
                ErrorOutputPreview = errorPreview,
                OutputUrl = payload.OutputUrl,
                ErrorUrl = payload.ErrorUrl,
                LogTruncated = payload.LogTruncated
            });
        }

        var subscriptionId = webJob.App.SubscriptionId ?? string.Empty;
        var subscriptionName = _subscriptions.FirstOrDefault(s => s.Value.SubscriptionId == subscriptionId).Key ?? subscriptionId;

        return new WebJobDrillDownViewModel
        {
            WebJobId = webJob.Id,
            WebJobName = webJob.Name,
            AppId = webJob.App.Id,
            AppName = webJob.App.Name,
            SubscriptionId = subscriptionId,
            SubscriptionName = subscriptionName,
            JobType = webJob.JobType,
            Status = webJob.Status,
            Schedule = webJob.Schedule,
            RunMode = webJob.RunMode,
            RecentRuns = runs
        };
    }

    public async Task<DependencyCurrencyViewModel> GetDependencyCurrencyAsync(string subscriptionId)
    {
        var subscriptionName = _subscriptions.FirstOrDefault(s => s.Value.SubscriptionId == subscriptionId).Key ?? subscriptionId;

        // Get apps filtered by subscription
        var apps = await _dbContext.Apps
            .Where(a => a.IsEnabled)
            .Where(a => a.SubscriptionId == subscriptionId)
            .Select(a => new { a.Id, a.Name, a.Importance, a.RepoId })
            .ToListAsync();

        var repoIds = apps.Where(a => a.RepoId.HasValue).Select(a => a.RepoId!.Value).ToList();

        // Get dependencies for these repos
        var dependencies = await (
            from dep in _dbContext.Dependencies
            where repoIds.Contains(dep.Manifest.RepoId)
            let pkgVer = dep.PackageVersion
            let pkg = pkgVer.Package
            let manifest = dep.Manifest
            let repo = manifest.Repo
            let platform = pkg.Platform
            select new
            {
                RepoId = repo.Id,
                RepoName = repo.Name,
                PlatformName = platform.Name,
                PackageName = pkg.Name,
                CurrentVersion = pkgVer.Version,
                CurrentMajor = pkgVer.Major,
                CurrentMinor = pkgVer.Minor,
                PackageId = pkg.Id
            }
        ).ToListAsync();

        // Get latest versions for each package
        var packageIds = dependencies.Select(d => d.PackageId).Distinct().ToList();
        var latestVersions = await _dbContext.PackageVersions
            .Where(pv => packageIds.Contains(pv.PackageId))
            .GroupBy(pv => pv.PackageId)
            .Select(g => new
            {
                PackageId = g.Key,
                LatestMajor = g.Where(v => string.IsNullOrEmpty(v.PreRelease))
                    .OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor).ThenByDescending(v => v.Patch)
                    .Select(v => v.Version)
                    .FirstOrDefault(),
                LatestMajorPrerelease = g.Where(v => !string.IsNullOrEmpty(v.PreRelease))
                    .OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor).ThenByDescending(v => v.Patch)
                    .Select(v => v.Version)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var latestVersionDict = latestVersions.ToDictionary(v => v.PackageId);

        var currencyModels = new List<DependencyCurrencyModel>();

        foreach (var dep in dependencies)
        {
            var app = apps.FirstOrDefault(a => a.RepoId == dep.RepoId);
            if (app == null) continue;

            if (!latestVersionDict.TryGetValue(dep.PackageId, out var latest))
                continue;

            // Calculate latest minor version for current major
            var latestMinor = await _dbContext.PackageVersions
                .Where(v => v.PackageId == dep.PackageId)
                .Where(v => v.Major == dep.CurrentMajor)
                .Where(v => string.IsNullOrEmpty(v.PreRelease))
                .OrderByDescending(v => v.Minor).ThenByDescending(v => v.Patch)
                .Select(v => v.Version)
                .FirstOrDefaultAsync();

            var latestMinorPrerelease = await _dbContext.PackageVersions
                .Where(v => v.PackageId == dep.PackageId)
                .Where(v => v.Major == dep.CurrentMajor)
                .Where(v => !string.IsNullOrEmpty(v.PreRelease))
                .OrderByDescending(v => v.Minor).ThenByDescending(v => v.Patch)
                .Select(v => v.Version)
                .FirstOrDefaultAsync();

            // Calculate severity and priority score
            var isBehindMajor = latest.LatestMajor != null && latest.LatestMajor != dep.CurrentVersion;
            var isBehindMinor = latestMinor != null && latestMinor != dep.CurrentVersion;

            var severityLevel = "Low";
            var deltaSeverity = 1.0;

            if (isBehindMajor)
            {
                severityLevel = "High";
                deltaSeverity = 3.0;
            }
            else if (isBehindMinor)
            {
                severityLevel = "Medium";
                deltaSeverity = 2.0;
            }

            // Priority score = importance × delta severity
            var priorityScore = app.Importance * deltaSeverity;

            currencyModels.Add(new DependencyCurrencyModel
            {
                AppId = app.Id,
                AppName = app.Name,
                AppImportance = app.Importance,
                SubscriptionId = subscriptionId,
                SubscriptionName = subscriptionName,
                PlatformName = dep.PlatformName,
                PackageName = dep.PackageName,
                CurrentVersion = dep.CurrentVersion,
                LatestMajorVersion = latest.LatestMajor,
                LatestMinorVersion = latestMinor,
                LatestMajorPrerelease = latest.LatestMajorPrerelease,
                LatestMinorPrerelease = latestMinorPrerelease,
                PriorityScore = priorityScore,
                SeverityLevel = severityLevel
            });
        }

        return new DependencyCurrencyViewModel
        {
            SubscriptionId = subscriptionId,
            SubscriptionName = subscriptionName,
            Dependencies = currencyModels.OrderByDescending(d => d.PriorityScore).ToList()
        };
    }
}
