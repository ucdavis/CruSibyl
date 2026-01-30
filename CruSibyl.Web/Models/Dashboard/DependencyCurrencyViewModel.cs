namespace CruSibyl.Web.Models.Dashboard;

public class DependencyCurrencyViewModel
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string SubscriptionName { get; set; } = string.Empty;
    public List<DependencyCurrencyModel> Dependencies { get; set; } = new();
}

public class DependencyCurrencyModel
{
    public int AppId { get; set; }
    public string AppName { get; set; } = null!;
    public double AppImportance { get; set; }
    public string SubscriptionId { get; set; } = string.Empty;
    public string SubscriptionName { get; set; } = string.Empty;
    public string PlatformName { get; set; } = null!;
    public string PackageName { get; set; } = null!;
    public string CurrentVersion { get; set; } = null!;
    public string? LatestMajorVersion { get; set; }
    public string? LatestMinorVersion { get; set; }
    public string? LatestMajorPrerelease { get; set; }
    public string? LatestMinorPrerelease { get; set; }
    public double PriorityScore { get; set; }
    public string SeverityLevel { get; set; } = "Low"; // Low, Medium, High, Critical
}
