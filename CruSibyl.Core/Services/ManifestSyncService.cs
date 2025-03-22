using System.Diagnostics;
using System.Text.Json;
using CruSibyl.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace CruSibyl.Core.Services;

public interface IManifestSyncService
{
    public Task<Result> SyncManifests();
}

public class ManifestSyncService : IManifestSyncService
{
    private readonly IGitHubService _gitHubService;
    private readonly AppDbContext _dbContext;

    public ManifestSyncService(IGitHubService gitHubService, AppDbContext dbContext)
    {
        _gitHubService = gitHubService;
        _dbContext = dbContext;
    }

    public async Task<Result> SyncManifests()
    {
        var repos = await _dbContext.Repos.ToArrayAsync();

        foreach (var repo in repos)
        {
            Debug.WriteLine($"Getting {repo.Name} manifests");
            var manifests = await _gitHubService.GetManifests(repo.Name);
            foreach (var manifest in manifests)
            {
                Debug.WriteLine(JsonSerializer.Serialize(manifest, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            }
        }

        return Result.Ok();
    }
}