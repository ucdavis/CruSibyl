namespace CruSibyl.Web.Models.Dashboard;

public class DashboardViewModel
{
    public DateTime CapturedAtUtc { get; set; }

    public DashboardWindowModel Windows { get; set; } = new();

    public WebJobOverviewModel WebJobs { get; set; } = new();

    public PlatformOverviewModel Platform { get; set; } = new();

    public DependencyOverviewModel Dependency { get; set; } = new();

    public AppMappingOverviewModel AppMappings { get; set; } = new();

    public DataFreshnessOverviewModel DataFreshness { get; set; } = new();

    public List<AppHealthCardModel> AppHealthCards { get; set; } = new();

    public List<CriticalAlert> CriticalAlerts { get; set; } = new();
}

public class DashboardWindowModel
{
    public int ConsecutiveFailureAttempts { get; set; }

    public int RecentFailureWindowHours { get; set; }

    public int HistoryWindowDays { get; set; }

    public int StaleScanDays { get; set; }

    public DateTime RecentFailureWindowStartUtc { get; set; }

    public DateTime HistoryWindowStartUtc { get; set; }

    public DateTime StaleScanCutoffUtc { get; set; }
}

public class WebJobOverviewModel
{
    public int TotalEnabledJobs { get; set; }

    public int RunningJobs { get; set; }

    public int JobsWithFailedLastRun { get; set; }

    public int JobsRecentlyFailed { get; set; }

    public int JobsFailingConsecutiveAttempts { get; set; }
}

public class PlatformOverviewModel
{
    public int EnabledAppsWithManifests { get; set; }

    public int AppsOnOutdatedManifestPlatformVersions { get; set; }

    public int ReposOnOutdatedManifestPlatformVersions { get; set; }

    public int UnknownPlatformVersionData { get; set; }

    public int FailedManifestScans { get; set; }

    public int StaleManifestScans { get; set; }
}

public class DependencyOverviewModel
{
    public int TotalTrackedDependencies { get; set; }

    public int OutdatedDependencies { get; set; }

    public int MajorBehindDependencies { get; set; }

    public int MinorBehindDependencies { get; set; }

    public int PatchBehindDependencies { get; set; }

    public int UnknownCurrencyDependencies { get; set; }

    public int HighPriorityOutdatedDependencies { get; set; }

    public int EnabledAppsWithDependencyCoverage { get; set; }

    public int EnabledAppsWithoutDependencyCoverage { get; set; }
}

public class AppMappingOverviewModel
{
    public int EnabledApps { get; set; }

    public int EnabledAppsWithMappedRepos { get; set; }

    public int UnmappedEnabledApps { get; set; }

    public int MappedAppsWithManifestlessRepos { get; set; }

    public int MappedAppsWithStaleRepoScans { get; set; }

    public int MappedAppsWithFailedRepoScans { get; set; }
}

public class DataFreshnessOverviewModel
{
    public int StaleRepoScans { get; set; }

    public int FailedRepoScans { get; set; }

    public int StalePackageScans { get; set; }

    public int FailedPackageScans { get; set; }

    public int AppsWithoutRepos { get; set; }

    public int ReposWithoutManifests { get; set; }
}

public enum DashboardMetricTone
{
    Neutral,
    Info,
    Success,
    Warning,
    Error
}

public class DashboardMetricModel
{
    public string Label { get; set; } = null!;

    public int Count { get; set; }

    public string? Url { get; set; }

    public string IconClass { get; set; } = "fas fa-circle";

    public DashboardMetricTone Tone { get; set; } = DashboardMetricTone.Neutral;

    public string Description { get; set; } = null!;

    public bool OpensWhenZero { get; set; }

    public bool CanOpen => !string.IsNullOrWhiteSpace(Url) && (Count > 0 || OpensWhenZero);
}

public class AppHealthCardModel
{
    public int AppId { get; set; }
    public string AppName { get; set; } = null!;
    public string? ResourceGroup { get; set; }
    public double Importance { get; set; }
    public int TotalWebJobs { get; set; }
    public int RunningWebJobs { get; set; }
    public int FailedWebJobs { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public string? LastFailureJobName { get; set; }
    public bool HasCriticalFailure { get; set; }
}

public class CriticalAlert
{
    public int AppId { get; set; }
    public string AppName { get; set; } = null!;
    public int WebJobId { get; set; }
    public string WebJobName { get; set; } = null!;
    public DateTime FailureTime { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsConsistent { get; set; }
}
