namespace CruSibyl.Web.Models.Dashboard;

using CruSibyl.Web.Configuration;

public class DashboardViewModel
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string SubscriptionName { get; set; } = string.Empty;
    public Dictionary<string, AzureSubscription> Subscriptions { get; set; } = new();
    public List<AppHealthCardModel> AppHealthCards { get; set; } = new();
    public List<CriticalAlert> CriticalAlerts { get; set; } = new();
}

public class AppHealthCardModel
{
    public int AppId { get; set; }
    public string AppName { get; set; } = null!;
    public string? ResourceGroup { get; set; }
    public string? SubscriptionId { get; set; }
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
