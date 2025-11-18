using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace CruSibyl.Core.Services;

public interface IEventService
{
    /// <summary>
    /// Log an event related to apps, WebJobs, and/or repos
    /// </summary>
    Task LogEvent(
        EventType eventType, 
        string message, 
        string severity = "Info", 
        int[]? appIds = null, 
        int[]? webJobIds = null, 
        int[]? repoIds = null, 
        string? details = null, 
        string? correlationId = null,
        string? source = null,
        CancellationToken cancellationToken = default);
}

public class EventService : IEventService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public EventService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task LogEvent(
        EventType eventType, 
        string message, 
        string severity = "Info",
        int[]? appIds = null, 
        int[]? webJobIds = null, 
        int[]? repoIds = null,
        string? details = null, 
        string? correlationId = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var eventEntity = new Event
        {
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            Severity = severity,
            Message = message,
            Details = details,
            Source = source ?? "System",
            CorrelationId = correlationId
        };

        // Load related entities
        if (appIds != null && appIds.Length > 0)
        {
            var apps = await dbContext.Apps.Where(a => appIds.Contains(a.Id)).ToListAsync(cancellationToken);
            eventEntity.Apps.AddRange(apps);
        }

        if (webJobIds != null && webJobIds.Length > 0)
        {
            var webJobs = await dbContext.WebJobs.Where(wj => webJobIds.Contains(wj.Id)).ToListAsync(cancellationToken);
            eventEntity.WebJobs.AddRange(webJobs);
        }

        if (repoIds != null && repoIds.Length > 0)
        {
            var repos = await dbContext.Repos.Where(r => repoIds.Contains(r.Id)).ToListAsync(cancellationToken);
            eventEntity.Repos.AddRange(repos);
        }

        dbContext.Events.Add(eventEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        Log.Information("Logged event {EventType}: {Message}", eventType, message);
    }
}
