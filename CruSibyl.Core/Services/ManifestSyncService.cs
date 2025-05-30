using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Semver;
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
        // TODO: This scan metadata logic is kind of messy. Consider moving the data to a separate table
        var scanInfo = await _dbContext.Repos
            .GroupBy(r => 1)
            .Select(g => new
            {
                MaxScanNumber = g.Max(r => r.ScanNumber ?? 0),
                AllSameScanNumber = g.Select(r => r.ScanNumber ?? 0).Distinct().Count() == 1,
                AllCompleted = g.All(r => r.ScanStatus == ScanStatus.Completed)
            })
            .FirstAsync();

        var scanNumber = scanInfo.MaxScanNumber;
        var scanComplete = scanInfo.AllSameScanNumber && scanInfo.AllCompleted;

        Repo[] repos = Array.Empty<Repo>();

        if (scanComplete || scanNumber == 0)
        {
            // If all repos are complete, increment ScanNumber and start a new batch
            scanNumber += 1;
            repos = await _dbContext.Repos.ToArrayAsync();
            foreach (var repo in repos)
            {
                repo.ScanNumber = scanNumber;
                repo.ScanStatus = ScanStatus.InProgress;
            }
            await _dbContext.SaveChangesAsync();
        }
        else if (!scanInfo.AllSameScanNumber)
        {
            // This can happen if some repos have been recently added or updated
            scanNumber = Math.Max(scanNumber, 1);
            repos = await _dbContext.Repos
                .Where(r => (r.ScanNumber ?? 0) != scanNumber)
                .ToArrayAsync();
            foreach (var repo in repos)
            {
                repo.ScanNumber = scanNumber;
                repo.ScanStatus = ScanStatus.InProgress;
            }
            await _dbContext.SaveChangesAsync();
        }

        // Get only repos that are not complete for the current batch
        repos = await _dbContext.Repos
            .Where(r => (r.ScanNumber ?? 0) == scanNumber && r.ScanStatus != ScanStatus.Completed)
            .ToArrayAsync();

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

        try
        {
            foreach (var repo in repos)
            {
                Log.Verbose("Getting {RepoName} manifests", repo.Name);
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
                            _dbContext.Packages.Add(package);
                        }

                        // Ensure PackageVersion exists
                        var packageVersion = package.Versions.FirstOrDefault(v => v.Version == dep.Version);
                        if (packageVersion == null)
                        {
                            var baseVersion = ExtractBaseVersion(dep.Version);
                            var version = SemVersion.Parse(baseVersion);
                            packageVersion = new PackageVersion
                            {
                                Package = package,
                                Version = dep.Version,
                                Major = (int?)(version?.Major),
                                Minor = (int?)(version?.Minor),
                                Patch = (int?)(version?.Patch),
                                PreRelease = version?.Prerelease,
                            };
                            package.Versions.Add(packageVersion);
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
                            _dbContext.Dependencies.Add(newDependency);
                        }
                    }
                }
                repo.ScanStatus = ScanStatus.Completed;
                await _dbContext.SaveChangesAsync();
            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error Syncing");
            return Result.Error(ex.Message);
        }
    }

    private static string ExtractBaseVersion(string versionSpec)
    {
        // Match the first version-like pattern (e.g., 1.2.3, 1.2.3-beta)
        var match = Regex.Match(versionSpec, @"\d+\.\d+\.\d+(-[A-Za-z0-9\.-]+)?");
        return match.Success ? match.Value : versionSpec;
    }
}