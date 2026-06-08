using CruSibyl.Web;

namespace CruSibyl.Tests;

public class HtmxComponentContractTests
{
    [Fact]
    public void CruSibylTablesHaveStableScopedComponentIds()
    {
        var ids = new[]
        {
            TableComponentIds.AdminRepos,
            TableComponentIds.AdminApps,
            TableComponentIds.AdminUsers,
            TableComponentIds.ReportPackageVersions,
        };

        Assert.Equal(
            ["hc-table-admin-repos", "hc-table-admin-apps", "hc-table-admin-users", "hc-table-report-package-versions"],
            ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AdminAndReportsViewsRenderSharedScopedTableComponent()
    {
        var viewPaths = new[]
        {
            "CruSibyl.Web/Views/Admin/_Repos.cshtml",
            "CruSibyl.Web/Views/Admin/_Apps.cshtml",
            "CruSibyl.Web/Views/Admin/_AdminUsers.cshtml",
            "CruSibyl.Web/Views/Reports/_PackageVersions.cshtml",
        };

        foreach (var viewPath in viewPaths)
        {
            var view = ReadRepoFile(viewPath);

            Assert.Matches("""Component\.InvokeAsync\(\s*"Table"\s*,""", view);
            Assert.DoesNotContain("<table", view, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void DashboardPartialsUseLifecycleAwareRequestAndErrorContract()
    {
        var content = ReadRepoFile("CruSibyl.Web/Views/Dashboard/_Content.cshtml");
        var failureHistory = ReadRepoFile("CruSibyl.Web/Views/Dashboard/_FailureHistory.cshtml");
        var dashboardScript = ReadRepoFile("CruSibyl.Web/wwwroot/js/dashboard.js");
        var dashboardViews = Directory
            .EnumerateFiles(Path.Combine(FindRepoRoot(), "CruSibyl.Web/Views/Dashboard"), "*.cshtml")
            .Select(File.ReadAllText)
            .ToArray();

        Assert.Contains("id=\"dashboard-detail-scope\"", content, StringComparison.Ordinal);
        Assert.Contains("data-hc-request-indicator-selector", content, StringComparison.Ordinal);
        Assert.Contains("data-hc-stale-region-selector", content, StringComparison.Ordinal);
        Assert.Contains("<htmx-error-region", content, StringComparison.Ordinal);
        Assert.Contains("id=\"dashboard-detail-content\"", content, StringComparison.Ordinal);

        Assert.Contains("hx-target=\"#dashboard-detail-content\"", failureHistory, StringComparison.Ordinal);
        Assert.DoesNotContain(
            dashboardViews,
            view => view.Contains("hx-target=\"#dashboard-detail\"", StringComparison.Ordinal));

        Assert.Contains("htmx-components:load", dashboardScript, StringComparison.Ordinal);
        Assert.Contains("htmx:beforeCleanupElement", dashboardScript, StringComparison.Ordinal);
        Assert.Contains("canvas[data-dashboard-sparkline]", dashboardScript, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", string.Join(Environment.NewLine, dashboardViews), StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadRepoFile(string path)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), path));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CruSibyl.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the CruSibyl repository root.");
    }
}
