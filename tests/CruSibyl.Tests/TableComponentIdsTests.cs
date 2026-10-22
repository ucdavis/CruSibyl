using CruSibyl.Web;
using CruSibyl.Web.Models.Dashboard;

namespace CruSibyl.Tests;

public class TableComponentIdsTests
{
    [Fact]
    public void StaticTableIds_AreStableAndDistinct()
    {
        var ids = new[]
        {
            TableComponentIds.AdminRepos,
            TableComponentIds.AdminApps,
            TableComponentIds.AdminUsers,
            TableComponentIds.ReportPackageVersions,
        };

        Assert.Equal(
            [
                "hc-table-admin-repos",
                "hc-table-admin-apps",
                "hc-table-admin-users",
                "hc-table-report-package-versions",
            ],
            ids);
        AssertDistinct(ids);
    }

    [Fact]
    public void DashboardFilterTableIds_AreStableAndDistinct()
    {
        var ids = new[]
        {
            TableComponentIds.DashboardWebJobDetails(WebJobDetailFilter.RecentlyFailed),
            TableComponentIds.DashboardPlatformDetails(PlatformDetailFilter.Outdated),
            TableComponentIds.DashboardDependencyDetails(DependencyDetailFilter.Major),
            TableComponentIds.DashboardAppMappingDetails(AppMappingDetailFilter.Unmapped),
            TableComponentIds.DashboardDataFreshnessDetails(DataFreshnessDetailFilter.FailedPackageScans),
        };

        Assert.Equal(
            [
                "hc-table-dashboard-webjob-details-recentlyfailed",
                "hc-table-dashboard-platform-details-outdated",
                "hc-table-dashboard-dependency-details-major",
                "hc-table-dashboard-app-mapping-details-unmapped",
                "hc-table-dashboard-data-freshness-details-failedpackagescans",
            ],
            ids);
        AssertDistinct(ids);
    }

    [Theory]
    [InlineData(null, null, "hc-table-dashboard-failure-history-app-none-job-none")]
    [InlineData("Giving", "Nightly Sync", "hc-table-dashboard-failure-history-app-476976696e67-job-4e696768746c792053796e63")]
    [InlineData(" Giving ", " Nightly Sync ", "hc-table-dashboard-failure-history-app-476976696e67-job-4e696768746c792053796e63")]
    public void DashboardFailureHistoryTableId_RoundTripsFilters(
        string? appFilter,
        string? jobFilter,
        string expectedComponentId)
    {
        var componentId = TableComponentIds.DashboardFailureHistory(appFilter, jobFilter);

        Assert.Equal(expectedComponentId, componentId);
        Assert.True(TableComponentIds.TryParseDashboardFailureHistoryFilters(componentId, out var parsedAppFilter, out var parsedJobFilter));
        Assert.Equal(string.IsNullOrWhiteSpace(appFilter) ? null : appFilter.Trim(), parsedAppFilter);
        Assert.Equal(string.IsNullOrWhiteSpace(jobFilter) ? null : jobFilter.Trim(), parsedJobFilter);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hc-table-admin-repos")]
    [InlineData("hc-table-dashboard-failure-history-app-not-hex-job-none")]
    [InlineData("hc-table-dashboard-failure-history-app-none")]
    public void TryParseDashboardFailureHistoryFilters_RejectsNonFailureHistoryIds(string? componentId)
    {
        Assert.False(TableComponentIds.TryParseDashboardFailureHistoryFilters(componentId, out var appFilter, out var jobFilter));
        Assert.Null(appFilter);
        Assert.Null(jobFilter);
    }

    private static void AssertDistinct(string[] ids)
        => Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
}
