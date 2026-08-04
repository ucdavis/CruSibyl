using System.Text;
using Htmx.Components.Table;
using CruSibyl.Web.Models.Dashboard;

namespace CruSibyl.Web;

public static class TableComponentIds
{
    public static string AdminRepos => TableComponentIdentity.Ensure("admin-repos");
    public static string AdminApps => TableComponentIdentity.Ensure("admin-apps");
    public static string AdminUsers => TableComponentIdentity.Ensure("admin-users");
    public static string ReportPackageVersions => TableComponentIdentity.Ensure("report-package-versions");
    public static string DashboardWebJobDetails(WebJobDetailFilter filter) => TableComponentIdentity.Ensure($"dashboard-webjob-details-{NormalizeFilter(filter)}");
    public static string DashboardPlatformDetails(PlatformDetailFilter filter) => TableComponentIdentity.Ensure($"dashboard-platform-details-{NormalizeFilter(filter)}");
    public static string DashboardDependencyDetails(DependencyDetailFilter filter) => TableComponentIdentity.Ensure($"dashboard-dependency-details-{NormalizeFilter(filter)}");
    public static string DashboardAppMappingDetails(AppMappingDetailFilter filter) => TableComponentIdentity.Ensure($"dashboard-app-mapping-details-{NormalizeFilter(filter)}");
    public static string DashboardDataFreshnessDetails(DataFreshnessDetailFilter filter) => TableComponentIdentity.Ensure($"dashboard-data-freshness-details-{NormalizeFilter(filter)}");
    public static string DashboardFailureHistory(string? appFilter, string? jobFilter)
        => TableComponentIdentity.Ensure($"dashboard-failure-history-app-{EncodeTextFilter(appFilter)}-job-{EncodeTextFilter(jobFilter)}");

    public static bool TryParseDashboardFailureHistoryFilters(string? componentId, out string? appFilter, out string? jobFilter)
    {
        appFilter = null;
        jobFilter = null;

        if (string.IsNullOrWhiteSpace(componentId))
        {
            return false;
        }

        var ensuredComponentId = TableComponentIdentity.Ensure(componentId);
        const string prefix = "hc-table-dashboard-failure-history-app-";
        const string separator = "-job-";

        if (!ensuredComponentId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var encodedFilters = ensuredComponentId[prefix.Length..];
        var separatorIndex = encodedFilters.IndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return false;
        }

        if (!TryDecodeTextFilter(encodedFilters[..separatorIndex], out appFilter)
            || !TryDecodeTextFilter(encodedFilters[(separatorIndex + separator.Length)..], out jobFilter))
        {
            appFilter = null;
            jobFilter = null;
            return false;
        }

        return true;
    }

    private static string NormalizeFilter<TFilter>(TFilter filter)
        where TFilter : struct, Enum
        => filter.ToString().ToLowerInvariant();

    private static string EncodeTextFilter(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "none"
            : Convert.ToHexString(Encoding.UTF8.GetBytes(value.Trim())).ToLowerInvariant();

    private static bool TryDecodeTextFilter(string token, out string? value)
    {
        value = null;

        if (token == "none")
        {
            return true;
        }

        if (token.Length == 0)
        {
            return false;
        }

        try
        {
            value = Encoding.UTF8.GetString(Convert.FromHexString(token));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public static class DashboardTableTypeIds
{
    public const string WebJobDetails = "DashboardWebJobDetail";
    public const string PlatformDetails = "DashboardPlatformDetail";
    public const string DependencyDetails = "DashboardDependencyDetail";
    public const string AppMappingDetails = "DashboardAppMappingDetail";
    public const string DataFreshnessDetails = "DashboardDataFreshnessDetail";
    public const string FailureHistory = "DashboardFailureHistory";
}
