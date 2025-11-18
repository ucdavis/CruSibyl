using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Domain;

[Index(nameof(EventType))]
[Index(nameof(Timestamp))]
[Index(nameof(Severity))]
public class Event
{
    public int Id { get; set; }

    [Required]
    public EventType EventType { get; set; }

    [Required]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Severity level: Info, Warning, Error, Critical
    /// </summary>
    [Required, MaxLength(50)]
    public string Severity { get; set; } = "Info";

    [Required, MaxLength(500)]
    public string Message { get; set; } = null!;

    /// <summary>
    /// Additional details in JSON format
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// User or system that triggered the event
    /// </summary>
    [MaxLength(255)]
    public string? Source { get; set; }

    /// <summary>
    /// Correlation ID for tracking related events
    /// </summary>
    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    // Many-to-many relationships
    public List<App> Apps { get; set; } = new();
    public List<WebJob> WebJobs { get; set; } = new();
    public List<Repo> Repos { get; set; } = new();

    internal static void OnModelCreating(ModelBuilder builder)
    {
        // Configure many-to-many relationships for Events
        builder.Entity<Event>()
            .HasMany(e => e.Apps)
            .WithMany()
            .UsingEntity(j => j.ToTable("EventApps"));

        builder.Entity<Event>()
            .HasMany(e => e.WebJobs)
            .WithMany()
            .UsingEntity(j => j.ToTable("EventWebJobs"));

        builder.Entity<Event>()
            .HasMany(e => e.Repos)
            .WithMany()
            .UsingEntity(j => j.ToTable("EventRepos"));
    }
}
