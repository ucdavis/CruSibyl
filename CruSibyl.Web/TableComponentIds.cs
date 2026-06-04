using Htmx.Components.Table;

namespace CruSibyl.Web;

public static class TableComponentIds
{
    public static string AdminRepos => TableComponentIdentity.Ensure("admin-repos");
    public static string AdminApps => TableComponentIdentity.Ensure("admin-apps");
    public static string AdminUsers => TableComponentIdentity.Ensure("admin-users");
    public static string ReportPackageVersions => TableComponentIdentity.Ensure("report-package-versions");
}
