using CruSibyl.Core.Domain;
using Htmx.Components.Table.Models;

namespace CruSibyl.Web.Models.Dashboard;

public enum WebJobDetailFilter
{
    TotalEnabled,
    Running,
    FailedLastRun,
    RecentlyFailed,
    ConsecutiveFailures
}

public enum PlatformDetailFilter
{
    WithManifests,
    Outdated,
    Unknown,
    FailedScans,
    StaleScans
}

public enum DependencyDetailFilter
{
    All,
    Outdated,
    Major,
    Minor,
    Patch,
    Unknown,
    HighPriority
}

public enum AppMappingDetailFilter
{
    Unmapped,
    Manifestless,
    StaleRepoScans,
    FailedRepoScans
}

public enum DataFreshnessDetailFilter
{
    StaleRepoScans,
    FailedRepoScans,
    StalePackageScans,
    FailedPackageScans,
    AppsWithoutRepos,
    ReposWithoutManifests
}

public class WebJobDetailsViewModel
{
    public WebJobDetailFilter Filter { get; set; }

    public string Title { get; set; } = null!;

    public List<WebJobDetailRowModel> Rows { get; set; } = new();
}

public class DashboardDetailTableViewModel
{
    public string Title { get; set; } = null!;

    public string EmptyMessage { get; set; } = "No rows match this filter.";

    public ITableModel Table { get; set; } = null!;
}

public class WebJobDetailRowModel
{
    public int WebJobId { get; set; }

    public string AppName { get; set; } = null!;

    public string WebJobName { get; set; } = null!;

    public string JobType { get; set; } = null!;

    public string? Status { get; set; }

    public string? LastRunStatus { get; set; }

    public DateTime? LastRunAt { get; set; }

    public string? RunMode { get; set; }

    public string? Schedule { get; set; }
}

public class PlatformDetailsViewModel
{
    public PlatformDetailFilter Filter { get; set; }

    public string Title { get; set; } = null!;

    public List<PlatformDetailRowModel> Rows { get; set; } = new();
}

public class PlatformDetailRowModel
{
    public string RowKey { get; set; } = null!;

    public string AppName { get; set; } = null!;

    public string? RepoName { get; set; }

    public DateTime? RepoLastScannedAt { get; set; }

    public ScanStatus? RepoScanStatus { get; set; }

    public string? ManifestPath { get; set; }

    public string? PlatformName { get; set; }

    public string? CurrentVersion { get; set; }

    public string? LatestStableVersion { get; set; }

    public string CurrencyStatus { get; set; } = "Unknown";
}

public class AppMappingDetailsViewModel
{
    public AppMappingDetailFilter Filter { get; set; }

    public string Title { get; set; } = null!;

    public List<AppMappingDetailRowModel> Rows { get; set; } = new();
}

public class AppMappingDetailRowModel
{
    public int AppId { get; set; }

    public string AppName { get; set; } = null!;

    public string? ResourceGroup { get; set; }

    public string? RuntimeStack { get; set; }

    public int? RepoId { get; set; }

    public string? RepoName { get; set; }

    public DateTime? RepoLastScannedAt { get; set; }

    public ScanStatus? RepoScanStatus { get; set; }

    public int ManifestCount { get; set; }

    public List<string> CandidateRepoNames { get; set; } = new();
}

public class DataFreshnessDetailsViewModel
{
    public DataFreshnessDetailFilter Filter { get; set; }

    public string Title { get; set; } = null!;

    public List<DataFreshnessDetailRowModel> Rows { get; set; } = new();
}

public class DataFreshnessDetailRowModel
{
    public string RowKey { get; set; } = null!;

    public string ItemType { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? RelatedName { get; set; }

    public DateTime? LastScannedAt { get; set; }

    public ScanStatus? ScanStatus { get; set; }

    public string? ScanMessage { get; set; }

    public string? Detail { get; set; }
}
