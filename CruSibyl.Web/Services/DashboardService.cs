using System.Text.Json;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Models;
using CruSibyl.Core.Models.Settings;
using CruSibyl.Web.Models.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Semver;

namespace CruSibyl.Web.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardOverviewAsync();
    Task<WebJobDetailsViewModel> GetWebJobDetailsAsync(WebJobDetailFilter filter);
    Task<PlatformDetailsViewModel> GetPlatformDetailsAsync(PlatformDetailFilter filter);
    Task<DependencyCurrencyViewModel> GetDependencyDetailsAsync(DependencyDetailFilter filter);
    Task<AppMappingDetailsViewModel> GetAppMappingDetailsAsync(AppMappingDetailFilter filter);
    Task<DataFreshnessDetailsViewModel> GetDataFreshnessDetailsAsync(DataFreshnessDetailFilter filter);
    Task<WebJobFailureHistoryViewModel> GetFailureHistoryAsync(string? appFilter = null, string? jobFilter = null);
    Task<WebJobDrillDownViewModel> GetWebJobDrillDownAsync(int webJobId);
    Task<DependencyCurrencyViewModel> GetDependencyCurrencyAsync();
}

public class DashboardService : IDashboardService
{
    private const double MajorDependencySeverity = 3.0;
    private const double MinorDependencySeverity = 2.0;
    private const double PatchDependencySeverity = 1.0;
    private const double HighPriorityDependencyScore = 1.5;

    private readonly AppDbContext _dbContext;
    private readonly DashboardSettings _dashboardSettings;
    private readonly TimeProvider _timeProvider;

    public DashboardService(
        AppDbContext dbContext,
        IOptions<DashboardSettings> dashboardSettings,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _dashboardSettings = dashboardSettings.Value;
        _timeProvider = timeProvider;
    }

    public async Task<DashboardViewModel> GetDashboardOverviewAsync()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var windows = CreateDashboardWindowModel(now);
        
        var apps = await _dbContext.Apps
            .Include(a => a.WebJobs)
            .AsNoTracking()
            .Where(a => a.IsEnabled)
            .Where(a => a.WebJobs.Any(wj => wj.IsEnabled))
            .ToListAsync();

        var webJobIds = apps.SelectMany(a => a.WebJobs).Select(wj => wj.Id).ToList();

        // Get recent failure events for these apps
        var recentFailures = await _dbContext.Events
            .Where(e => e.EventType == EventType.WebJobStatus)
            .Where(e => e.Timestamp >= windows.HistoryWindowStartUtc)
            .Where(e => e.Severity == "Error" || e.Severity == "Critical")
            .Where(e => e.WebJobs.Any(wj => webJobIds.Contains(wj.Id)))
            .Include(e => e.WebJobs)
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync();

        var webJobOverview = await GetWebJobOverviewAsync(webJobIds, windows);
        var appMappingOverview = await GetAppMappingOverviewAsync(windows);
        var dataFreshnessOverview = await GetDataFreshnessOverviewAsync(windows);
        var platformOverview = await GetPlatformOverviewAsync(windows);
        var dependencyOverview = await GetDependencyOverviewAsync();

        var appHealthCards = new List<AppHealthCardModel>();
        var criticalAlerts = new List<CriticalAlert>();

        foreach (var app in apps)
        {
            var appWebJobs = app.WebJobs.Where(wj => wj.IsEnabled).ToList();
            var appWebJobIds = appWebJobs.Select(wj => wj.Id).ToHashSet();
            var appFailures = recentFailures.Where(e => e.WebJobs.Any(wj => appWebJobIds.Contains(wj.Id))).ToList();

            var lastFailure = appFailures.FirstOrDefault();
            var lastFailureWebJob = lastFailure?.WebJobs.FirstOrDefault(wj => appWebJobIds.Contains(wj.Id));

            // Check for repeated failures within the configured recent-failure window.
            var recentFailureCount = appFailures.Count(e => e.Timestamp >= windows.RecentFailureWindowStartUtc);
            var hasCriticalFailure = recentFailureCount >= windows.ConsecutiveFailureAttempts;

            if (hasCriticalFailure && lastFailure != null && lastFailureWebJob != null)
            {
                var payload = lastFailure.Payload.HasValue 
                    ? JsonSerializer.Deserialize<WebJobStatusPayload>(lastFailure.Payload.Value.GetRawText())
                    : null;

                // Preserve the existing consistency heuristic until the summary-query pass revisits it.
                var recentWeekFailures = appFailures.Count(e => e.Timestamp >= windows.RecentFailureWindowStartUtc && e.WebJobs.Any(wj => wj.Id == lastFailureWebJob.Id));
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
            CapturedAtUtc = now,
            Windows = windows,
            WebJobs = webJobOverview,
            Platform = platformOverview,
            Dependency = dependencyOverview,
            AppMappings = appMappingOverview,
            DataFreshness = dataFreshnessOverview,
            AppHealthCards = appHealthCards.OrderByDescending(a => a.HasCriticalFailure).ThenByDescending(a => a.Importance).ToList(),
            CriticalAlerts = criticalAlerts.OrderByDescending(a => a.FailureTime).ToList()
        };
    }

    public async Task<WebJobFailureHistoryViewModel> GetFailureHistoryAsync(string? appFilter = null, string? jobFilter = null)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var windows = CreateDashboardWindowModel(now);

        var query = _dbContext.Events
            .Where(e => e.EventType == EventType.WebJobStatus)
            .Where(e => e.Timestamp >= windows.HistoryWindowStartUtc)
            .Where(e => e.Severity == "Error" || e.Severity == "Critical")
            .Include(e => e.WebJobs)
                .ThenInclude(wj => wj.App)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(appFilter))
        {
            query = query.Where(e => e.WebJobs.Any(wj => wj.App.Name.Contains(appFilter)));
        }

        if (!string.IsNullOrWhiteSpace(jobFilter))
        {
            query = query.Where(e => e.WebJobs.Any(wj => wj.Name.Contains(jobFilter)));
        }

        var events = await query.OrderByDescending(e => e.Timestamp).Take(100).ToListAsync();
        var eventRuns = events
            .Select(evt =>
            {
                var payload = DeserializeWebJobStatusPayload(evt);
                return new
                {
                    Event = evt,
                    Payload = payload,
                    RunTimestamp = GetWebJobRunTimestamp(evt, payload)
                };
            })
            .OrderByDescending(run => run.RunTimestamp)
            .ToList();

        var failures = new List<WebJobFailureHistoryModel>();
        var webJobFailureCounts = new Dictionary<int, List<int>>();

        foreach (var eventRun in eventRuns)
        {
            var evt = eventRun.Event;
            var webJob = evt.WebJobs.FirstOrDefault();
            if (webJob == null) continue;

            var payload = eventRun.Payload;

            // Calculate if this is a consistent failure
            var recentFailures = eventRuns.Count(e =>
                e.Event.WebJobs.Any(wj => wj.Id == webJob.Id) &&
                e.RunTimestamp >= eventRun.RunTimestamp.AddDays(-7));
            var isConsistent = recentFailures > 5;

            failures.Add(new WebJobFailureHistoryModel
            {
                WebJobId = webJob.Id,
                WebJobName = webJob.Name,
                AppId = webJob.App.Id,
                AppName = webJob.App.Name,
                FailureTime = eventRun.RunTimestamp,
                Status = payload?.Status ?? "Failed",
                DurationMs = payload?.DurationMs,
                ErrorOutput = payload?.ErrorOutput,
                IsConsistent = isConsistent
            });

            // Build sparkline data (30 daily buckets)
            if (!webJobFailureCounts.ContainsKey(webJob.Id))
            {
                var dailyCounts = new List<int>();
                for (int i = 0; i < windows.HistoryWindowDays; i++)
                {
                    var dayStart = windows.HistoryWindowStartUtc.AddDays(i);
                    var dayEnd = dayStart.AddDays(1);
                    var count = eventRuns.Count(e =>
                        e.Event.WebJobs.Any(wj => wj.Id == webJob.Id) &&
                        e.RunTimestamp >= dayStart &&
                        e.RunTimestamp < dayEnd);
                    dailyCounts.Add(count);
                }
                webJobFailureCounts[webJob.Id] = dailyCounts;
            }
        }

        return new WebJobFailureHistoryViewModel
        {
            AppFilter = appFilter,
            JobFilter = jobFilter,
            Failures = failures,
            FailureSparklines = webJobFailureCounts
        };
    }

    public async Task<WebJobDetailsViewModel> GetWebJobDetailsAsync(WebJobDetailFilter filter)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var windows = CreateDashboardWindowModel(now);

        var webJobs = await _dbContext.WebJobs
            .Include(wj => wj.App)
            .Where(wj => wj.IsEnabled && wj.App.IsEnabled)
            .AsNoTracking()
            .ToListAsync();

        var selectedWebJobIds = webJobs.Select(wj => wj.Id).ToHashSet();

        if ((filter is WebJobDetailFilter.RecentlyFailed or WebJobDetailFilter.ConsecutiveFailures) && selectedWebJobIds.Count > 0)
        {
            var webJobRunEvents = await _dbContext.Events
                .Where(e => e.EventType == EventType.WebJobStatus)
                .Where(e => e.Timestamp >= windows.HistoryWindowStartUtc)
                .Where(e => e.WebJobs.Any(wj => selectedWebJobIds.Contains(wj.Id)))
                .Include(e => e.WebJobs)
                .AsNoTracking()
                .ToListAsync();

            var webJobRuns = webJobRunEvents
                .SelectMany(evt =>
                {
                    var payload = DeserializeWebJobStatusPayload(evt);
                    var runTimestamp = GetWebJobRunTimestamp(evt, payload);
                    return evt.WebJobs
                        .Where(wj => selectedWebJobIds.Contains(wj.Id))
                        .Select(wj => new WebJobRunSummary(wj.Id, runTimestamp, IsFailedWebJobRun(evt, payload)));
                })
                .ToList();

            selectedWebJobIds = filter == WebJobDetailFilter.RecentlyFailed
                ? webJobRuns
                    .Where(run => run.RunTimestamp >= windows.RecentFailureWindowStartUtc && run.IsFailure)
                    .Select(run => run.WebJobId)
                    .ToHashSet()
                : webJobRuns
                    .GroupBy(run => run.WebJobId)
                    .Where(group =>
                    {
                        var latestRuns = group
                            .OrderByDescending(run => run.RunTimestamp)
                            .Take(windows.ConsecutiveFailureAttempts)
                            .ToList();

                        return latestRuns.Count == windows.ConsecutiveFailureAttempts && latestRuns.All(run => run.IsFailure);
                    })
                    .Select(group => group.Key)
                    .ToHashSet();
        }

        var rows = webJobs
            .Where(webJob => filter switch
            {
                WebJobDetailFilter.Running => IsStatus(webJob.Status, "Running"),
                WebJobDetailFilter.FailedLastRun => IsStatus(webJob.LastRunStatus, "Failed"),
                WebJobDetailFilter.RecentlyFailed => selectedWebJobIds.Contains(webJob.Id),
                WebJobDetailFilter.ConsecutiveFailures => selectedWebJobIds.Contains(webJob.Id),
                _ => true
            })
            .OrderBy(webJob => webJob.App.Name)
            .ThenBy(webJob => webJob.Name)
            .Select(webJob => new WebJobDetailRowModel
            {
                WebJobId = webJob.Id,
                AppName = webJob.App.Name,
                WebJobName = webJob.Name,
                JobType = webJob.JobType,
                Status = webJob.Status,
                LastRunStatus = webJob.LastRunStatus,
                LastRunAt = webJob.LastRunAt,
                RunMode = webJob.RunMode,
                Schedule = webJob.Schedule
            })
            .ToList();

        return new WebJobDetailsViewModel
        {
            Filter = filter,
            Title = GetWebJobDetailTitle(filter),
            Rows = rows
        };
    }

    public async Task<PlatformDetailsViewModel> GetPlatformDetailsAsync(PlatformDetailFilter filter)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var windows = CreateDashboardWindowModel(now);
        var rows = await GetPlatformDetailRowsAsync(windows);

        rows = rows
            .Where(row => filter switch
            {
                PlatformDetailFilter.Outdated => row.CurrencyStatus == "Outdated",
                PlatformDetailFilter.Unknown => row.CurrencyStatus == "Unknown",
                PlatformDetailFilter.FailedScans => row.RepoScanStatus == ScanStatus.Failed,
                PlatformDetailFilter.StaleScans => IsStale(row.RepoLastScannedAt, windows),
                _ => row.ManifestPath != null
            })
            .OrderBy(row => row.AppName)
            .ThenBy(row => row.RepoName)
            .ThenBy(row => row.ManifestPath)
            .ToList();

        return new PlatformDetailsViewModel
        {
            Filter = filter,
            Title = GetPlatformDetailTitle(filter),
            Rows = rows
        };
    }

    public async Task<DependencyCurrencyViewModel> GetDependencyDetailsAsync(DependencyDetailFilter filter)
    {
        var rows = await GetDependencyCurrencyRowsAsync();

        rows = rows
            .Where(row => filter switch
            {
                DependencyDetailFilter.Outdated => IsOutdatedDependencySeverity(row.SeverityLevel),
                DependencyDetailFilter.Major => row.SeverityLevel == "High",
                DependencyDetailFilter.Minor => row.SeverityLevel == "Medium",
                DependencyDetailFilter.Patch => row.SeverityLevel == "Low" && row.PriorityScore > 0,
                DependencyDetailFilter.Unknown => row.SeverityLevel == "Unknown",
                DependencyDetailFilter.HighPriority => IsOutdatedDependencySeverity(row.SeverityLevel) && row.PriorityScore >= HighPriorityDependencyScore,
                _ => true
            })
            .OrderByDescending(row => row.PriorityScore)
            .ThenBy(row => row.AppName)
            .ThenBy(row => row.PackageName)
            .ToList();

        return new DependencyCurrencyViewModel
        {
            Dependencies = rows
        };
    }

    public async Task<AppMappingDetailsViewModel> GetAppMappingDetailsAsync(AppMappingDetailFilter filter)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var windows = CreateDashboardWindowModel(now);

        var apps = await _dbContext.Apps
            .Where(a => a.IsEnabled)
            .Select(a => new AppMappingDetailRowModel
            {
                AppId = a.Id,
                AppName = a.Name,
                ResourceGroup = a.ResourceGroup,
                RuntimeStack = a.RuntimeStack,
                RepoId = a.RepoId,
                RepoName = a.Repo == null ? null : a.Repo.Name,
                RepoLastScannedAt = a.Repo == null ? null : a.Repo.LastScannedAt,
                RepoScanStatus = a.Repo == null ? null : a.Repo.ScanStatus,
                ManifestCount = a.Repo == null ? 0 : a.Repo.Manifests.Count
            })
            .ToListAsync();

        var repoNames = await _dbContext.Repos
            .Select(repo => repo.Name)
            .ToListAsync();

        var rows = apps
            .Where(row => filter switch
            {
                AppMappingDetailFilter.Manifestless => row.RepoId.HasValue && row.ManifestCount == 0,
                AppMappingDetailFilter.StaleRepoScans => row.RepoId.HasValue && IsStale(row.RepoLastScannedAt, windows),
                AppMappingDetailFilter.FailedRepoScans => row.RepoId.HasValue && row.RepoScanStatus == ScanStatus.Failed,
                _ => !row.RepoId.HasValue
            })
            .OrderBy(row => row.AppName)
            .ToList();

        foreach (var row in rows.Where(row => !row.RepoId.HasValue))
        {
            row.CandidateRepoNames = FindCandidateRepoNames(row.AppName, repoNames);
        }

        return new AppMappingDetailsViewModel
        {
            Filter = filter,
            Title = GetAppMappingDetailTitle(filter),
            Rows = rows
        };
    }

    public async Task<DataFreshnessDetailsViewModel> GetDataFreshnessDetailsAsync(DataFreshnessDetailFilter filter)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var windows = CreateDashboardWindowModel(now);
        var rows = new List<DataFreshnessDetailRowModel>();

        if (filter is DataFreshnessDetailFilter.StaleRepoScans or DataFreshnessDetailFilter.FailedRepoScans)
        {
            var repoRows = await _dbContext.Repos
                .Select(repo => new
                {
                    repo.Name,
                    repo.LastScannedAt,
                    repo.ScanStatus,
                    repo.ScanMessage,
                    ManifestCount = repo.Manifests.Count
                })
                .ToListAsync();

            rows.AddRange(repoRows
                .Where(repo => filter == DataFreshnessDetailFilter.StaleRepoScans
                    ? IsStale(repo.LastScannedAt, windows)
                    : repo.ScanStatus == ScanStatus.Failed)
                .Select(repo => new DataFreshnessDetailRowModel
                {
                    ItemType = "Repo",
                    Name = repo.Name,
                    LastScannedAt = repo.LastScannedAt,
                    ScanStatus = repo.ScanStatus,
                    ScanMessage = repo.ScanMessage,
                    Detail = $"{repo.ManifestCount} manifest(s)"
                }));
        }
        else if (filter is DataFreshnessDetailFilter.StalePackageScans or DataFreshnessDetailFilter.FailedPackageScans)
        {
            var packageRows = await _dbContext.Packages
                .Select(package => new
                {
                    package.Name,
                    PlatformName = package.Platform.Name,
                    package.LastScannedAt,
                    package.ScanStatus,
                    package.ScanMessage
                })
                .ToListAsync();

            rows.AddRange(packageRows
                .Where(package => filter == DataFreshnessDetailFilter.StalePackageScans
                    ? IsStale(package.LastScannedAt, windows)
                    : package.ScanStatus == ScanStatus.Failed)
                .Select(package => new DataFreshnessDetailRowModel
                {
                    ItemType = "Package",
                    Name = package.Name,
                    RelatedName = package.PlatformName,
                    LastScannedAt = package.LastScannedAt,
                    ScanStatus = package.ScanStatus,
                    ScanMessage = package.ScanMessage
                }));
        }
        else if (filter == DataFreshnessDetailFilter.AppsWithoutRepos)
        {
            rows.AddRange(await _dbContext.Apps
                .Where(app => app.IsEnabled && !app.RepoId.HasValue)
                .OrderBy(app => app.Name)
                .Select(app => new DataFreshnessDetailRowModel
                {
                    ItemType = "App",
                    Name = app.Name,
                    RelatedName = app.ResourceGroup,
                    Detail = app.RuntimeStack
                })
                .ToListAsync());
        }
        else
        {
            rows.AddRange(await _dbContext.Repos
                .Where(repo => repo.Manifests.Count == 0)
                .OrderBy(repo => repo.Name)
                .Select(repo => new DataFreshnessDetailRowModel
                {
                    ItemType = "Repo",
                    Name = repo.Name,
                    LastScannedAt = repo.LastScannedAt,
                    ScanStatus = repo.ScanStatus,
                    ScanMessage = repo.ScanMessage,
                    Detail = "No manifests"
                })
                .ToListAsync());
        }

        return new DataFreshnessDetailsViewModel
        {
            Filter = filter,
            Title = GetDataFreshnessDetailTitle(filter),
            Rows = rows
                .OrderBy(row => row.ItemType)
                .ThenBy(row => row.Name)
                .ToList()
        };
    }

    public async Task<WebJobDrillDownViewModel> GetWebJobDrillDownAsync(int webJobId)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var windows = CreateDashboardWindowModel(now);

        var webJob = await _dbContext.WebJobs
            .Include(wj => wj.App)
            .AsNoTracking()
            .OrderBy(wj => wj.Id) // just to eliminate the warning about using First without OrderBy
            .FirstOrDefaultAsync(wj => wj.Id == webJobId);

        if (webJob == null)
        {
            throw new ArgumentException($"WebJob with ID {webJobId} not found");
        }

        var events = await _dbContext.Events
            .Where(e => e.EventType == EventType.WebJobStatus)
            .Where(e => e.WebJobs.Any(wj => wj.Id == webJobId))
            .Where(e => e.Timestamp >= windows.HistoryWindowStartUtc)
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .Take(50)
            .ToListAsync();

        var runs = new List<WebJobRunModel>();
        foreach (var evt in events
            .Select(e => new { Event = e, Payload = DeserializeWebJobStatusPayload(e) })
            .OrderByDescending(e => GetWebJobRunTimestamp(e.Event, e.Payload)))
        {
            var payload = evt.Payload;

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

        return new WebJobDrillDownViewModel
        {
            WebJobId = webJob.Id,
            WebJobName = webJob.Name,
            AppId = webJob.App.Id,
            AppName = webJob.App.Name,
            JobType = webJob.JobType,
            Status = webJob.Status,
            Schedule = webJob.Schedule,
            RunMode = webJob.RunMode,
            RecentRuns = runs
        };
    }

    public async Task<DependencyCurrencyViewModel> GetDependencyCurrencyAsync()
    {
        var currencyModels = await GetDependencyCurrencyRowsAsync();

        return new DependencyCurrencyViewModel
        {
            Dependencies = currencyModels.OrderByDescending(d => d.PriorityScore).ToList()
        };
    }

    private DashboardWindowModel CreateDashboardWindowModel(DateTime now)
    {
        var consecutiveFailureAttempts = Math.Max(1, _dashboardSettings.ConsecutiveFailureAttempts);
        var recentFailureWindowHours = Math.Max(1, _dashboardSettings.RecentFailureWindowHours);
        var historyWindowDays = Math.Max(1, _dashboardSettings.HistoryWindowDays);
        var staleScanDays = Math.Max(1, _dashboardSettings.StaleScanDays);

        return new DashboardWindowModel
        {
            ConsecutiveFailureAttempts = consecutiveFailureAttempts,
            RecentFailureWindowHours = recentFailureWindowHours,
            HistoryWindowDays = historyWindowDays,
            StaleScanDays = staleScanDays,
            RecentFailureWindowStartUtc = now.AddHours(-recentFailureWindowHours),
            HistoryWindowStartUtc = now.AddDays(-historyWindowDays),
            StaleScanCutoffUtc = now.AddDays(-staleScanDays)
        };
    }

    private async Task<WebJobOverviewModel> GetWebJobOverviewAsync(IReadOnlyCollection<int> webJobIds, DashboardWindowModel windows)
    {
        var enabledWebJobs = await _dbContext.WebJobs
            .Where(wj => wj.IsEnabled && wj.App.IsEnabled)
            .Select(wj => new
            {
                wj.Id,
                wj.Status,
                wj.LastRunStatus
            })
            .ToListAsync();

        if (enabledWebJobs.Count == 0)
        {
            return new WebJobOverviewModel();
        }

        var enabledWebJobIds = enabledWebJobs.Select(wj => wj.Id).ToHashSet();
        var eventWebJobIds = webJobIds.Count > 0
            ? webJobIds.Where(enabledWebJobIds.Contains).ToList()
            : enabledWebJobIds.ToList();

        var webJobRunEvents = await _dbContext.Events
            .Where(e => e.EventType == EventType.WebJobStatus)
            .Where(e => e.Timestamp >= windows.HistoryWindowStartUtc)
            .Where(e => e.WebJobs.Any(wj => eventWebJobIds.Contains(wj.Id)))
            .Include(e => e.WebJobs)
            .AsNoTracking()
            .ToListAsync();

        var webJobRuns = webJobRunEvents
            .SelectMany(evt =>
            {
                var payload = DeserializeWebJobStatusPayload(evt);
                var runTimestamp = GetWebJobRunTimestamp(evt, payload);
                return evt.WebJobs
                    .Where(wj => enabledWebJobIds.Contains(wj.Id))
                    .Select(wj => new WebJobRunSummary(wj.Id, runTimestamp, IsFailedWebJobRun(evt, payload)));
            })
            .ToList();

        var recentlyFailedJobIds = webJobRuns
            .Where(run => run.RunTimestamp >= windows.RecentFailureWindowStartUtc && run.IsFailure)
            .Select(run => run.WebJobId)
            .Distinct()
            .Count();

        var consecutiveFailures = webJobRuns
            .GroupBy(run => run.WebJobId)
            .Count(group =>
            {
                var latestRuns = group
                    .OrderByDescending(run => run.RunTimestamp)
                    .Take(windows.ConsecutiveFailureAttempts)
                    .ToList();

                return latestRuns.Count == windows.ConsecutiveFailureAttempts && latestRuns.All(run => run.IsFailure);
            });

        return new WebJobOverviewModel
        {
            TotalEnabledJobs = enabledWebJobs.Count,
            RunningJobs = enabledWebJobs.Count(wj => IsStatus(wj.Status, "Running")),
            JobsWithFailedLastRun = enabledWebJobs.Count(wj => IsStatus(wj.LastRunStatus, "Failed")),
            JobsRecentlyFailed = recentlyFailedJobIds,
            JobsFailingConsecutiveAttempts = consecutiveFailures
        };
    }

    private async Task<AppMappingOverviewModel> GetAppMappingOverviewAsync(DashboardWindowModel windows)
    {
        var enabledApps = await _dbContext.Apps
            .Where(a => a.IsEnabled)
            .Select(a => new
            {
                a.Id,
                a.RepoId,
                RepoLastScannedAt = a.Repo == null ? null : a.Repo.LastScannedAt,
                RepoScanStatus = a.Repo == null ? null : a.Repo.ScanStatus,
                ManifestCount = a.Repo == null ? 0 : a.Repo.Manifests.Count
            })
            .ToListAsync();

        return new AppMappingOverviewModel
        {
            EnabledApps = enabledApps.Count,
            EnabledAppsWithMappedRepos = enabledApps.Count(a => a.RepoId.HasValue),
            UnmappedEnabledApps = enabledApps.Count(a => !a.RepoId.HasValue),
            MappedAppsWithManifestlessRepos = enabledApps.Count(a => a.RepoId.HasValue && a.ManifestCount == 0),
            MappedAppsWithStaleRepoScans = enabledApps.Count(a => a.RepoId.HasValue && IsStale(a.RepoLastScannedAt, windows)),
            MappedAppsWithFailedRepoScans = enabledApps.Count(a => a.RepoId.HasValue && a.RepoScanStatus == ScanStatus.Failed)
        };
    }

    private async Task<DataFreshnessOverviewModel> GetDataFreshnessOverviewAsync(DashboardWindowModel windows)
    {
        var repoScanCounts = await _dbContext.Repos
            .Select(repo => new
            {
                repo.LastScannedAt,
                repo.ScanStatus,
                ManifestCount = repo.Manifests.Count
            })
            .ToListAsync();

        var packageScanCounts = await _dbContext.Packages
            .Select(package => new
            {
                package.LastScannedAt,
                package.ScanStatus
            })
            .ToListAsync();

        var appsWithoutRepos = await _dbContext.Apps
            .Where(a => a.IsEnabled && !a.RepoId.HasValue)
            .CountAsync();

        return new DataFreshnessOverviewModel
        {
            StaleRepoScans = repoScanCounts.Count(repo => IsStale(repo.LastScannedAt, windows)),
            FailedRepoScans = repoScanCounts.Count(repo => repo.ScanStatus == ScanStatus.Failed),
            StalePackageScans = packageScanCounts.Count(package => IsStale(package.LastScannedAt, windows)),
            FailedPackageScans = packageScanCounts.Count(package => package.ScanStatus == ScanStatus.Failed),
            AppsWithoutRepos = appsWithoutRepos,
            ReposWithoutManifests = repoScanCounts.Count(repo => repo.ManifestCount == 0)
        };
    }

    private async Task<PlatformOverviewModel> GetPlatformOverviewAsync(DashboardWindowModel windows)
    {
        var enabledApps = await _dbContext.Apps
            .Where(a => a.IsEnabled)
            .Select(a => new
            {
                a.Id,
                a.RepoId,
                RepoLastScannedAt = a.Repo == null ? null : a.Repo.LastScannedAt,
                RepoScanStatus = a.Repo == null ? null : a.Repo.ScanStatus,
                ManifestCount = a.Repo == null ? 0 : a.Repo.Manifests.Count
            })
            .ToListAsync();

        var mappedRepoIds = enabledApps
            .Where(app => app.RepoId.HasValue)
            .Select(app => app.RepoId!.Value)
            .Distinct()
            .ToList();

        var manifests = await _dbContext.Manifests
            .Where(manifest => mappedRepoIds.Contains(manifest.RepoId))
            .Select(manifest => new
            {
                manifest.RepoId,
                manifest.PlatformVersion.PlatformId,
                manifest.PlatformVersion.Version
            })
            .ToListAsync();

        var platformVersions = await _dbContext.PlatformVersions
            .Select(version => new
            {
                version.PlatformId,
                version.Version
            })
            .ToListAsync();

        var latestStablePlatformVersions = platformVersions
            .Select(version => new
            {
                version.PlatformId,
                ParsedVersion = TryParseSemVersion(version.Version)
            })
            .Where(version => version.ParsedVersion is { IsPrerelease: false })
            .GroupBy(version => version.PlatformId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(version => version.ParsedVersion!)
                    .OrderByDescending(version => version, SemVersion.PrecedenceComparer)
                    .First());

        var manifestCurrency = manifests
            .Select(manifest =>
            {
                var currentVersion = TryParseSemVersion(manifest.Version);
                var hasLatestVersion = latestStablePlatformVersions.TryGetValue(manifest.PlatformId, out var latestVersion);
                var isUnknown = currentVersion == null || !hasLatestVersion;
                var isOutdated = !isUnknown && currentVersion!.ComparePrecedenceTo(latestVersion!) < 0;

                return new
                {
                    manifest.RepoId,
                    IsUnknown = isUnknown,
                    IsOutdated = isOutdated
                };
            })
            .ToList();

        var outdatedRepoIds = manifestCurrency
            .Where(manifest => manifest.IsOutdated)
            .Select(manifest => manifest.RepoId)
            .Distinct()
            .ToHashSet();

        var unknownRepoIds = manifestCurrency
            .Where(manifest => manifest.IsUnknown)
            .Select(manifest => manifest.RepoId)
            .Distinct()
            .ToHashSet();

        return new PlatformOverviewModel
        {
            EnabledAppsWithManifests = enabledApps.Count(app => app.ManifestCount > 0),
            AppsOnOutdatedManifestPlatformVersions = enabledApps.Count(app => app.RepoId.HasValue && outdatedRepoIds.Contains(app.RepoId.Value)),
            ReposOnOutdatedManifestPlatformVersions = outdatedRepoIds.Count,
            UnknownPlatformVersionData = enabledApps.Count(app => app.RepoId.HasValue && unknownRepoIds.Contains(app.RepoId.Value)),
            FailedManifestScans = enabledApps.Count(app => app.RepoId.HasValue && app.RepoScanStatus == ScanStatus.Failed),
            StaleManifestScans = enabledApps.Count(app => app.RepoId.HasValue && IsStale(app.RepoLastScannedAt, windows))
        };
    }

    private async Task<DependencyOverviewModel> GetDependencyOverviewAsync()
    {
        var enabledApps = await _dbContext.Apps
            .Where(a => a.IsEnabled)
            .Select(a => new
            {
                a.Id,
                a.RepoId,
                DependencyCount = a.Repo == null ? 0 : a.Repo.Manifests.SelectMany(manifest => manifest.Dependencies).Count()
            })
            .ToListAsync();

        var currencyRows = await GetDependencyCurrencyRowsAsync();
        var outdatedRows = currencyRows.Where(row => IsOutdatedDependencySeverity(row.SeverityLevel)).ToList();

        return new DependencyOverviewModel
        {
            TotalTrackedDependencies = currencyRows.Count,
            OutdatedDependencies = outdatedRows.Count,
            MajorBehindDependencies = currencyRows.Count(row => row.SeverityLevel == "High"),
            MinorBehindDependencies = currencyRows.Count(row => row.SeverityLevel == "Medium"),
            PatchBehindDependencies = currencyRows.Count(row => row.SeverityLevel == "Low" && row.PriorityScore > 0),
            UnknownCurrencyDependencies = currencyRows.Count(row => row.SeverityLevel == "Unknown"),
            HighPriorityOutdatedDependencies = outdatedRows.Count(row => row.PriorityScore >= HighPriorityDependencyScore),
            EnabledAppsWithDependencyCoverage = enabledApps.Count(app => app.DependencyCount > 0),
            EnabledAppsWithoutDependencyCoverage = enabledApps.Count(app => app.RepoId.HasValue && app.DependencyCount == 0)
        };
    }

    private async Task<List<PlatformDetailRowModel>> GetPlatformDetailRowsAsync(DashboardWindowModel windows)
    {
        var enabledApps = await _dbContext.Apps
            .Where(a => a.IsEnabled)
            .Select(a => new
            {
                a.Name,
                a.RepoId,
                RepoName = a.Repo == null ? null : a.Repo.Name,
                RepoLastScannedAt = a.Repo == null ? null : a.Repo.LastScannedAt,
                RepoScanStatus = a.Repo == null ? null : a.Repo.ScanStatus,
                ManifestCount = a.Repo == null ? 0 : a.Repo.Manifests.Count
            })
            .ToListAsync();

        var mappedRepoIds = enabledApps
            .Where(app => app.RepoId.HasValue)
            .Select(app => app.RepoId!.Value)
            .Distinct()
            .ToList();

        var manifests = await _dbContext.Manifests
            .Where(manifest => mappedRepoIds.Contains(manifest.RepoId))
            .Select(manifest => new
            {
                manifest.RepoId,
                manifest.FilePath,
                PlatformName = manifest.PlatformVersion.Platform.Name,
                manifest.PlatformVersion.PlatformId,
                manifest.PlatformVersion.Version
            })
            .ToListAsync();

        var platformVersions = await _dbContext.PlatformVersions
            .Select(version => new
            {
                version.PlatformId,
                version.Version
            })
            .ToListAsync();

        var latestStablePlatformVersions = platformVersions
            .Select(version => new
            {
                version.PlatformId,
                version.Version,
                ParsedVersion = TryParseSemVersion(version.Version)
            })
            .Where(version => version.ParsedVersion is { IsPrerelease: false })
            .GroupBy(version => version.PlatformId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(version => version.ParsedVersion!, SemVersion.PrecedenceComparer)
                    .First());

        var rows = new List<PlatformDetailRowModel>();
        var manifestsByRepo = manifests
            .GroupBy(manifest => manifest.RepoId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var app in enabledApps.Where(app => app.RepoId.HasValue))
        {
            if (!manifestsByRepo.TryGetValue(app.RepoId!.Value, out var repoManifests) || repoManifests.Count == 0)
            {
                rows.Add(new PlatformDetailRowModel
                {
                    AppName = app.Name,
                    RepoName = app.RepoName,
                    RepoLastScannedAt = app.RepoLastScannedAt,
                    RepoScanStatus = app.RepoScanStatus,
                    CurrencyStatus = "No manifests"
                });
                continue;
            }

            foreach (var manifest in repoManifests)
            {
                var currentVersion = TryParseSemVersion(manifest.Version);
                var hasLatestVersion = latestStablePlatformVersions.TryGetValue(manifest.PlatformId, out var latestVersion);
                var latestStableVersion = latestVersion?.ParsedVersion;
                var currencyStatus = currentVersion == null || !hasLatestVersion
                    ? "Unknown"
                    : currentVersion.ComparePrecedenceTo(latestStableVersion!) < 0
                        ? "Outdated"
                        : "Current";

                rows.Add(new PlatformDetailRowModel
                {
                    AppName = app.Name,
                    RepoName = app.RepoName,
                    RepoLastScannedAt = app.RepoLastScannedAt,
                    RepoScanStatus = app.RepoScanStatus,
                    ManifestPath = manifest.FilePath,
                    PlatformName = manifest.PlatformName,
                    CurrentVersion = manifest.Version,
                    LatestStableVersion = hasLatestVersion ? latestVersion?.Version : null,
                    CurrencyStatus = currencyStatus
                });
            }
        }

        return rows;
    }

    private async Task<List<DependencyCurrencyModel>> GetDependencyCurrencyRowsAsync()
    {
        var apps = await _dbContext.Apps
            .Where(a => a.IsEnabled)
            .Where(a => a.RepoId.HasValue)
            .Select(a => new DependencyAppSnapshot(a.Id, a.Name, a.Importance, a.RepoId!.Value))
            .ToListAsync();

        if (apps.Count == 0)
        {
            return new List<DependencyCurrencyModel>();
        }

        var repoIds = apps.Select(a => a.RepoId).Distinct().ToList();
        var dependencies = await (
            from dep in _dbContext.Dependencies
            where repoIds.Contains(dep.Manifest.RepoId)
            let pkgVer = dep.PackageVersion
            let pkg = pkgVer.Package
            let platform = pkg.Platform
            select new DependencySnapshot(
                dep.Manifest.RepoId,
                platform.Name,
                pkg.Name,
                pkgVer.Version,
                pkgVer.Major,
                pkgVer.Minor,
                pkgVer.Patch,
                pkg.Id)
        ).ToListAsync();

        if (dependencies.Count == 0)
        {
            return new List<DependencyCurrencyModel>();
        }

        var packageIds = dependencies.Select(d => d.PackageId).Distinct().ToList();
        var packageVersions = await _dbContext.PackageVersions
            .Where(version => packageIds.Contains(version.PackageId))
            .Select(version => new PackageVersionSnapshot(
                version.PackageId,
                version.Version,
                version.Major,
                version.Minor,
                version.Patch,
                version.PreRelease))
            .ToListAsync();

        var latestStableVersions = packageVersions
            .Where(IsComparableStableVersion)
            .GroupBy(version => version.PackageId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(version => version.Major)
                    .ThenByDescending(version => version.Minor)
                    .ThenByDescending(version => version.Patch)
                    .First());

        var latestPrereleaseVersions = packageVersions
            .Where(version => !string.IsNullOrEmpty(version.PreRelease) && HasComparableNumericParts(version))
            .GroupBy(version => version.PackageId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(version => version.Major)
                    .ThenByDescending(version => version.Minor)
                    .ThenByDescending(version => version.Patch)
                    .First());

        var latestStableByCurrentMajor = dependencies
            .Where(dependency => dependency.CurrentMajor.HasValue)
            .Select(dependency => new { dependency.PackageId, Major = dependency.CurrentMajor!.Value })
            .Distinct()
            .ToDictionary(
                dependency => (dependency.PackageId, dependency.Major),
                dependency => packageVersions
                    .Where(version =>
                        version.PackageId == dependency.PackageId &&
                        version.Major == dependency.Major &&
                        IsComparableStableVersion(version))
                    .OrderByDescending(version => version.Minor)
                    .ThenByDescending(version => version.Patch)
                    .FirstOrDefault());

        var latestPrereleaseByCurrentMajor = dependencies
            .Where(dependency => dependency.CurrentMajor.HasValue)
            .Select(dependency => new { dependency.PackageId, Major = dependency.CurrentMajor!.Value })
            .Distinct()
            .ToDictionary(
                dependency => (dependency.PackageId, dependency.Major),
                dependency => packageVersions
                    .Where(version =>
                        version.PackageId == dependency.PackageId &&
                        version.Major == dependency.Major &&
                        !string.IsNullOrEmpty(version.PreRelease) &&
                        HasComparableNumericParts(version))
                    .OrderByDescending(version => version.Minor)
                    .ThenByDescending(version => version.Patch)
                    .FirstOrDefault());

        var appsByRepo = apps
            .GroupBy(app => app.RepoId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var currencyModels = new List<DependencyCurrencyModel>();

        foreach (var dep in dependencies)
        {
            if (!appsByRepo.TryGetValue(dep.RepoId, out var repoApps))
            {
                continue;
            }

            latestStableVersions.TryGetValue(dep.PackageId, out var latestStable);
            latestPrereleaseVersions.TryGetValue(dep.PackageId, out var latestPrerelease);

            var latestStableCurrentMajor = dep.CurrentMajor.HasValue &&
                latestStableByCurrentMajor.TryGetValue((dep.PackageId, dep.CurrentMajor.Value), out var stableCurrentMajor)
                    ? stableCurrentMajor
                    : null;

            var latestPrereleaseCurrentMajor = dep.CurrentMajor.HasValue &&
                latestPrereleaseByCurrentMajor.TryGetValue((dep.PackageId, dep.CurrentMajor.Value), out var prereleaseCurrentMajor)
                    ? prereleaseCurrentMajor
                    : null;

            var classification = ClassifyDependency(dep, latestStable);

            foreach (var app in repoApps)
            {
                var priorityScore = app.Importance * classification.DeltaSeverity;

                currencyModels.Add(new DependencyCurrencyModel
                {
                    AppId = app.Id,
                    AppName = app.Name,
                    AppImportance = app.Importance,
                    PlatformName = dep.PlatformName,
                    PackageName = dep.PackageName,
                    CurrentVersion = dep.CurrentVersion,
                    LatestMajorVersion = latestStable?.Version,
                    LatestMinorVersion = latestStableCurrentMajor?.Version,
                    LatestMajorPrerelease = latestPrerelease?.Version,
                    LatestMinorPrerelease = latestPrereleaseCurrentMajor?.Version,
                    PriorityScore = classification.IsOutdated ? priorityScore : 0,
                    SeverityLevel = classification.SeverityLevel
                });
            }
        }

        return currencyModels;
    }

    private static DependencyClassification ClassifyDependency(DependencySnapshot dependency, PackageVersionSnapshot? latestStable)
    {
        if (!HasComparableNumericParts(dependency) || latestStable == null)
        {
            return new DependencyClassification("Unknown", 0, false);
        }

        if (latestStable.Major > dependency.CurrentMajor)
        {
            return new DependencyClassification("High", MajorDependencySeverity, true);
        }

        if (latestStable.Major == dependency.CurrentMajor && latestStable.Minor > dependency.CurrentMinor)
        {
            return new DependencyClassification("Medium", MinorDependencySeverity, true);
        }

        if (latestStable.Major == dependency.CurrentMajor &&
            latestStable.Minor == dependency.CurrentMinor &&
            latestStable.Patch > dependency.CurrentPatch)
        {
            return new DependencyClassification("Low", PatchDependencySeverity, true);
        }

        return new DependencyClassification("Current", 0, false);
    }

    private static bool IsOutdatedDependencySeverity(string severityLevel)
        => severityLevel is "High" or "Medium" or "Low";

    private static bool HasComparableNumericParts(DependencySnapshot dependency)
        => dependency.CurrentMajor.HasValue && dependency.CurrentMinor.HasValue && dependency.CurrentPatch.HasValue;

    private static bool HasComparableNumericParts(PackageVersionSnapshot version)
        => version.Major.HasValue && version.Minor.HasValue && version.Patch.HasValue;

    private static bool IsComparableStableVersion(PackageVersionSnapshot version)
        => string.IsNullOrEmpty(version.PreRelease) && HasComparableNumericParts(version);

    private static WebJobStatusPayload? DeserializeWebJobStatusPayload(Event evt)
        => evt.Payload.HasValue
            ? JsonSerializer.Deserialize<WebJobStatusPayload>(evt.Payload.Value.GetRawText())
            : null;

    private static DateTime GetWebJobRunTimestamp(Event evt, WebJobStatusPayload? payload)
        => payload?.StartTime ?? payload?.EndTime ?? evt.Timestamp;

    private static bool IsFailedWebJobRun(Event evt, WebJobStatusPayload? payload)
        => IsStatus(payload?.Status, "Failed") || evt.Severity is "Error" or "Critical";

    private static bool IsStatus(string? value, string expected)
        => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsStale(DateTime? lastScannedAt, DashboardWindowModel windows)
        => !lastScannedAt.HasValue || lastScannedAt.Value < windows.StaleScanCutoffUtc;

    private static SemVersion? TryParseSemVersion(string version)
        => SemVersion.TryParse(version, SemVersionStyles.Any, out var semVersion) ? semVersion : null;

    private static List<string> FindCandidateRepoNames(string appName, IReadOnlyCollection<string> repoNames)
    {
        var normalizedAppName = NormalizeName(appName);

        return repoNames
            .Where(repoName =>
            {
                var normalizedRepoName = NormalizeName(repoName);
                return normalizedRepoName.Contains(normalizedAppName, StringComparison.Ordinal) ||
                    normalizedAppName.Contains(normalizedRepoName, StringComparison.Ordinal);
            })
            .OrderBy(repoName => repoName)
            .Take(5)
            .ToList();
    }

    private static string NormalizeName(string value)
        => new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static string GetWebJobDetailTitle(WebJobDetailFilter filter)
        => filter switch
        {
            WebJobDetailFilter.Running => "Running WebJobs",
            WebJobDetailFilter.FailedLastRun => "WebJobs With Failed Last Run",
            WebJobDetailFilter.RecentlyFailed => "Recently Failed WebJobs",
            WebJobDetailFilter.ConsecutiveFailures => "WebJobs Failing Consecutive Attempts",
            _ => "Enabled WebJobs"
        };

    private static string GetPlatformDetailTitle(PlatformDetailFilter filter)
        => filter switch
        {
            PlatformDetailFilter.Outdated => "Outdated Platform Versions",
            PlatformDetailFilter.Unknown => "Unknown Platform Version Data",
            PlatformDetailFilter.FailedScans => "Failed Manifest Scans",
            PlatformDetailFilter.StaleScans => "Stale Manifest Scans",
            _ => "Apps With Manifest Platform Data"
        };

    private static string GetAppMappingDetailTitle(AppMappingDetailFilter filter)
        => filter switch
        {
            AppMappingDetailFilter.Manifestless => "Mapped Apps With Manifestless Repos",
            AppMappingDetailFilter.StaleRepoScans => "Mapped Apps With Stale Repo Scans",
            AppMappingDetailFilter.FailedRepoScans => "Mapped Apps With Failed Repo Scans",
            _ => "Unmapped Enabled Apps"
        };

    private static string GetDataFreshnessDetailTitle(DataFreshnessDetailFilter filter)
        => filter switch
        {
            DataFreshnessDetailFilter.FailedRepoScans => "Failed Repo Scans",
            DataFreshnessDetailFilter.StalePackageScans => "Stale Package Scans",
            DataFreshnessDetailFilter.FailedPackageScans => "Failed Package Scans",
            DataFreshnessDetailFilter.AppsWithoutRepos => "Apps Without Repos",
            DataFreshnessDetailFilter.ReposWithoutManifests => "Repos Without Manifests",
            _ => "Stale Repo Scans"
        };

    private sealed record WebJobRunSummary(int WebJobId, DateTime RunTimestamp, bool IsFailure);

    private sealed record DependencyAppSnapshot(int Id, string Name, double Importance, int RepoId);

    private sealed record DependencySnapshot(
        int RepoId,
        string PlatformName,
        string PackageName,
        string CurrentVersion,
        int? CurrentMajor,
        int? CurrentMinor,
        int? CurrentPatch,
        int PackageId);

    private sealed record PackageVersionSnapshot(
        int PackageId,
        string Version,
        int? Major,
        int? Minor,
        int? Patch,
        string? PreRelease);

    private sealed record DependencyClassification(string SeverityLevel, double DeltaSeverity, bool IsOutdated);
}
