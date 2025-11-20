namespace CruSibyl.Core.Models;

/// <summary>
/// Payload for WebJobStatus events
/// </summary>
public class WebJobStatusPayload
{
    public string? RunId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long? DurationMs { get; set; }
    public string? Status { get; set; }
    public string? LogOutput { get; set; }
    public string? ErrorOutput { get; set; }
    public bool LogTruncated { get; set; }
    public string? OutputUrl { get; set; }
    public string? ErrorUrl { get; set; }
}
