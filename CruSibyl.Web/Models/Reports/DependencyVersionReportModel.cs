namespace CruSibyl.Web.Models.Reports;


public class DependencyVersionReportModel
{
    public string RepoName { get; set; } = null!;
    public string PlatformName { get; set; } = null!;
    public string PkgName { get; set; } = null!;
    public string CurrentVersion { get; set; } = null!;
    public string? LatestMajorVersion { get; set; }
    public string? LatestMajorPrerelease { get; set; }
    public string? LatestMinorVersion { get; set; }
    public string? LatestMinorPrerelease { get; set; }
}