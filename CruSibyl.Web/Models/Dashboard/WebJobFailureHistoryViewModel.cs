namespace CruSibyl.Web.Models.Dashboard;

public class WebJobFailureHistoryModel
{
    public int WebJobId { get; set; }
    public string WebJobName { get; set; } = null!;
    public int AppId { get; set; }
    public string AppName { get; set; } = null!;
    public string SubscriptionId { get; set; } = string.Empty;
    public string SubscriptionName { get; set; } = string.Empty;
    public DateTime FailureTime { get; set; }
    public string Status { get; set; } = null!;
    public long? DurationMs { get; set; }
    public string? ErrorOutput { get; set; }
    public bool IsConsistent { get; set; }
}

public class WebJobFailureHistoryViewModel
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string SubscriptionName { get; set; } = string.Empty;
    public string? AppFilter { get; set; }
    public string? JobFilter { get; set; }
    public List<WebJobFailureHistoryModel> Failures { get; set; } = new();
    public Dictionary<int, List<int>> FailureSparklines { get; set; } = new(); // WebJobId -> daily failure counts (30 days)
}
