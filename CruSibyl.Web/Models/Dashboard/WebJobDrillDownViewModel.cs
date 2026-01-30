namespace CruSibyl.Web.Models.Dashboard;

public class WebJobDrillDownViewModel
{
    public int WebJobId { get; set; }
    public string WebJobName { get; set; } = null!;
    public int AppId { get; set; }
    public string AppName { get; set; } = null!;
    public string SubscriptionId { get; set; } = string.Empty;
    public string SubscriptionName { get; set; } = string.Empty;
    public string JobType { get; set; } = null!;
    public string? Status { get; set; }
    public string? Schedule { get; set; }
    public string? RunMode { get; set; }
    public List<WebJobRunModel> RecentRuns { get; set; } = new();
}

public class WebJobRunModel
{
    public string? RunId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long? DurationMs { get; set; }
    public string? Status { get; set; }
    public string? LogOutputPreview { get; set; }
    public string? ErrorOutputPreview { get; set; }
    public string? OutputUrl { get; set; }
    public string? ErrorUrl { get; set; }
    public bool LogTruncated { get; set; }
}
