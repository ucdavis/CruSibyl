using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(AppId), nameof(Name), IsUnique = true)]
[Index(nameof(JobType))]
[Index(nameof(Status))]
[Index(nameof(LastRunAt))]
public class WebJob
{
    public int Id { get; set; }

    [Required]
    public int AppId { get; set; }

    [ForeignKey(nameof(AppId))]
    public App App { get; set; } = null!;

    [Required, MaxLength(255)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Type of WebJob: Continuous or Triggered
    /// </summary>
    [Required, MaxLength(50)]
    public string JobType { get; set; } = null!;

    /// <summary>
    /// Current status: Running, Stopped, etc.
    /// </summary>
    [MaxLength(50)]
    public string? Status { get; set; }

    /// <summary>
    /// Run mode for triggered jobs (e.g., Scheduled, OnDemand)
    /// </summary>
    [MaxLength(50)]
    public string? RunMode { get; set; }

    /// <summary>
    /// Cron expression for scheduled jobs
    /// </summary>
    [MaxLength(100)]
    public string? Schedule { get; set; }

    /// <summary>
    /// URL to access WebJob details in Azure portal
    /// </summary>
    [MaxLength(500)]
    public string? Url { get; set; }

    /// <summary>
    /// URL for the WebJob's extra info endpoint
    /// </summary>
    [MaxLength(500)]
    public string? ExtraInfoUrl { get; set; }

    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// Status of the last run (Success, Failed, etc.)
    /// </summary>
    [MaxLength(50)]
    public string? LastRunStatus { get; set; }

    /// <summary>
    /// Duration of the last run in milliseconds
    /// </summary>
    public long? LastRunDurationMs { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool IsEnabled { get; set; } = true;
}
