using CruSibyl.Web.Models.Dashboard;

namespace CruSibyl.Tests;

public class DashboardUiRedesignTests
{
    [Fact]
    public void DashboardMetricModel_DisablesZeroCountMetricsByDefault()
    {
        var metric = new DashboardMetricModel
        {
            Label = "Failed scans",
            Count = 0,
            Url = "/Dashboard/Details/Freshness?filter=FailedRepoScans",
            Description = "Repos whose latest scan failed."
        };

        Assert.False(metric.CanOpen);
    }

    [Fact]
    public void DashboardMetricModel_CanOpenZeroCountMetricWhenEmptyStateIsUseful()
    {
        var metric = new DashboardMetricModel
        {
            Label = "Tracked dependencies",
            Count = 0,
            Url = "/Dashboard/Details/Dependencies?filter=All",
            Description = "All dependencies discovered from manifests.",
            OpensWhenZero = true
        };

        Assert.True(metric.CanOpen);
    }

    [Fact]
    public void DashboardContentView_UsesOverviewModulesAndModalTargets()
    {
        var contentView = ReadRepoFile("CruSibyl.Web/Views/Dashboard/_Content.cshtml");
        var metricPartial = ReadRepoFile("CruSibyl.Web/Views/Dashboard/_OverviewMetricGrid.cshtml");

        Assert.DoesNotContain("Model.AppHealthCards", contentView, StringComparison.Ordinal);
        Assert.DoesNotContain("CriticalAlerts", contentView, StringComparison.Ordinal);

        Assert.Contains("data-hc-dashboard-overview", contentView, StringComparison.Ordinal);
        Assert.Contains("id=\"dashboard-detail-scope\"", contentView, StringComparison.Ordinal);
        Assert.Contains("data-hc-request-indicator-selector=\"find [data-hc-detail-request-indicator]\"", contentView, StringComparison.Ordinal);
        Assert.Contains("<htmx-error-region class=\"alert alert-error\" hidden></htmx-error-region>", contentView, StringComparison.Ordinal);

        Assert.Contains("Dashboard/Details/WebJobs?filter=ConsecutiveFailures", contentView, StringComparison.Ordinal);
        Assert.Contains("Dashboard/Details/AppMappings?filter=Unmapped", contentView, StringComparison.Ordinal);
        Assert.Contains("Dashboard/Details/Dependencies?filter=HighPriority", contentView, StringComparison.Ordinal);
        Assert.Contains("Dashboard/Details/Freshness?filter=FailedPackageScans", contentView, StringComparison.Ordinal);
        Assert.Contains("Dashboard/FailureHistory", contentView, StringComparison.Ordinal);

        Assert.Contains("Id = \"dashboard-detail-modal\"", contentView, StringComparison.Ordinal);
        Assert.Contains("BodyTargetId = \"dashboard-detail-modal-body\"", contentView, StringComparison.Ordinal);
        Assert.Contains("hx-target=\"@(metric.CanOpen ? \"#dashboard-detail-modal-body\" : null)\"", metricPartial, StringComparison.Ordinal);
        Assert.Contains("data-hc-open-modal=\"@(metric.CanOpen ? \"dashboard-detail-modal\" : null)\"", metricPartial, StringComparison.Ordinal);
        Assert.Contains("disabled=\"@(!metric.CanOpen)\"", metricPartial, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CruSibyl.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find CruSibyl.sln from the test output directory.");
    }
}
