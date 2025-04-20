using System.Diagnostics;
using System.Text.Json;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Serilog;

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
        var platforms = await _dbContext.Platforms
            .Include(p => p.Versions)
            .ToDictionaryAsync(p => p.Name);
        var packages = await _dbContext.Packages
            .Include(p => p.Versions)
            .ToDictionaryAsync(p => $"{p.Name}|{p.PlatformId}");
        var manifests = await _dbContext.Manifests
            .ToDictionaryAsync(m => $"{m.RepoId}|{m.FilePath}");
        var dependencies = await _dbContext.Dependencies
            .ToDictionaryAsync(d => $"{d.ManifestId}|{d.PackageVersionId}");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            var newManifests = new List<Manifest>();
            var newPackages = new List<Package>();
            var newPackageVersions = new List<PackageVersion>();
            var newDependencies = new List<Dependency>();

            foreach (var repo in repos)
            {
                Debug.WriteLine($"Getting {repo.Name} manifests");
                var gitHubManifests = await _gitHubService.GetManifests(repo.Name);

                foreach (var gitHubManifest in gitHubManifests)
                {
                    // Ensure Platforms and PlatformVersions exist
                    if (!platforms.TryGetValue(gitHubManifest.Platform, out var platform))
                    {
                        platform = new Platform { Name = gitHubManifest.Platform, Versions = new() };
                        platforms[gitHubManifest.Platform] = platform;
                        _dbContext.Platforms.Add(platform);
                    }

                    var platformVersion = platform.Versions.FirstOrDefault(v => v.Version == gitHubManifest.PlatformVersion);
                    if (platformVersion == null)
                    {
                        platformVersion = new PlatformVersion
                        {
                            Platform = platform,
                            Version = gitHubManifest.PlatformVersion
                        };
                        platform.Versions.Add(platformVersion);
                        _dbContext.PlatformVersions.Add(platformVersion);
                    }

                    var manifestKey = $"{repo.Id}|{gitHubManifest.Path}";
                    if (!manifests.TryGetValue(manifestKey, out var manifest))
                    {
                        manifest = new Manifest
                        {
                            Repo = repo,
                            PlatformVersion = platformVersion,
                            FilePath = gitHubManifest.Path,
                            Dependencies = new()
                        };
                        manifests[manifestKey] = manifest;
                        newManifests.Add(manifest);
                        _dbContext.Manifests.Add(manifest);
                    }

                    foreach (var dep in gitHubManifest.Dependencies)
                    {
                        // Ensure Package exists
                        var packageKey = $"{dep.Name}|{platform.Id}";
                        if (!packages.TryGetValue(packageKey, out var package))
                        {
                            package = new Package { Name = dep.Name, Platform = platform, Versions = new() };
                            packages[packageKey] = package;
                            newPackages.Add(package);
                            _dbContext.Packages.Add(package);
                        }

                        // Ensure PackageVersion exists
                        var packageVersion = package.Versions.FirstOrDefault(v => v.Version == dep.Version);
                        if (packageVersion == null)
                        {
                            packageVersion = new PackageVersion
                            {
                                Package = package,
                                Version = dep.Version
                            };
                            package.Versions.Add(packageVersion);
                            newPackageVersions.Add(packageVersion);
                            _dbContext.PackageVersions.Add(packageVersion);
                        }

                        // Ensure Dependency exists
                        var dependencyKey = $"{manifest.Id}|{packageVersion.Id}";
                        if (!dependencies.ContainsKey(dependencyKey))
                        {
                            var newDependency = new Dependency
                            {
                                Manifest = manifest,
                                PackageVersion = packageVersion,
                                IsDevDependency = dep.IsDevDependency
                            };
                            newDependencies.Add(newDependency);
                            _dbContext.Dependencies.Add(newDependency);
                        }
                    }
                }
            }

            // Save all changes in bulk via EFCore.BulkExtensions
            await _dbContext.BulkSaveChangesAsync();

            await transaction.CommitAsync();
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error Syncing");
            await transaction.RollbackAsync();
            return Result.Error(ex.Message);
        }
    }

}