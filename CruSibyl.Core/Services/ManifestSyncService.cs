using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Models;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Semver;
using Serilog;

namespace CruSibyl.Core.Services;

public interface IManifestSyncService
{
    public Task<Result> SyncManifestsAsync(int timeLimitInMinutes);
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

    public async Task<Result> SyncManifestsAsync(int timeLimitInMinutes)
    {
        if (timeLimitInMinutes <= 1)
        {
            throw new ArgumentException("Time limit must be greater than 1 minute", nameof(timeLimitInMinutes));
        }
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
            .ToDictionaryAsync(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var packages = await _dbContext.Packages
            .Include(p => p.Versions)
            .Include(p => p.Platform)
            .ToDictionaryAsync(p => $"{p.Name}|{p.Platform.Name}", StringComparer.OrdinalIgnoreCase);
        var manifests = await _dbContext.Manifests
            .ToDictionaryAsync(m => $"{m.RepoId}|{m.FilePath}", StringComparer.OrdinalIgnoreCase);
        var dependencies = await _dbContext.Dependencies
            .ToDictionaryAsync(d => $"{d.ManifestId}|{d.PackageVersionId}", StringComparer.OrdinalIgnoreCase);

        try
        {
            var startTime = DateTime.UtcNow;
            TimeSpan timeLimit = TimeSpan.FromMinutes(timeLimitInMinutes);
            var exitingEarly = false;
            foreach (var repo in repos)
            {
                // Github throttles searches to two per minute. GetManifests performs two searches per repo. So 
                // we check if we are within 1 minute of the time limit.
                var elapsed = DateTime.UtcNow - startTime;
                if (elapsed >= timeLimit - TimeSpan.FromMinutes(1))
                {
                    Log.Information("Breaking out of repo sync loop: within 1 minute of time limit ({TimeLimit} min)", timeLimitInMinutes);
                    exitingEarly = true;
                    break;
                }
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
                        var packageKey = $"{dep.Name}|{platform.Name}";
                        if (!packages.TryGetValue(packageKey, out var package))
                        {
                            package = new Package { Name = dep.Name, Platform = platform, Versions = new() };
                            packages[packageKey] = package;
                            _dbContext.Packages.Add(package);
                        }

                        // Ensure PackageVersion exists
                        var packageVersion = package.Versions.FirstOrDefault(v => string.Equals(v.Version, dep.Version, StringComparison.OrdinalIgnoreCase));
                        if (packageVersion == null)
                        {
                            var baseVersion = ExtractBaseVersion(dep.Version);
                            SemVersion? version = null;
                            try
                            {
                                version = SemVersion.Parse(baseVersion);
                            }
                            catch (FormatException ex)
                            {
                                Log.Warning(ex, "Failed to parse baseVersion '{BaseVersion}' from '{VersionSpec}' for package '{Package}'", baseVersion, dep.Version, dep.Name);
                            }
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
                if (!exitingEarly)
                {
                    repo.ScanStatus = ScanStatus.Completed;
                }
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
        if (string.IsNullOrWhiteSpace(versionSpec))
            return versionSpec;

        // Remove common npm version range prefixes and operators
        var cleaned = versionSpec.Trim();

        // Handle special cases first
        if (cleaned.Equals("latest", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals("*", StringComparison.OrdinalIgnoreCase))
        {
            return "Unknown"; // Default fallback for non-specific versions
        }

        // Remove version range operators (^, ~, >=, <=, >, <, =)
        cleaned = Regex.Replace(cleaned, @"^[\^~>=<]*\s*", "");

        // Handle version ranges (e.g., "1.2.3 - 2.0.0" or ">=1.2.3 <2.0.0")
        // Take the first version in ranges
        var rangeMatch = Regex.Match(cleaned, @"^([0-9]+(?:\.[0-9]+)*(?:\.[0-9]+)?(?:-[A-Za-z0-9\.-]+)?)\s*(?:[-\s]|$)");
        if (rangeMatch.Success)
        {
            cleaned = rangeMatch.Groups[1].Value;
        }

        // Extract the core version pattern, being more permissive
        // This handles: 1.2.3, 1.2, 1, 1.2.3-beta.1, 1.2.3-alpha+build.1, etc.
        var versionMatch = Regex.Match(cleaned, @"^([0-9]+)(?:\.([0-9]+))?(?:\.([0-9]+))?(?:-([A-Za-z0-9\.-]+))?(?:\+([A-Za-z0-9\.-]+))?");

        if (versionMatch.Success)
        {
            var major = versionMatch.Groups[1].Value;
            var minor = versionMatch.Groups[2].Success ? versionMatch.Groups[2].Value : "0";
            var patch = versionMatch.Groups[3].Success ? versionMatch.Groups[3].Value : "0";
            var prerelease = versionMatch.Groups[4].Success ? $"-{versionMatch.Groups[4].Value}" : "";

            // Build a semver-compatible version
            return $"{major}.{minor}.{patch}{prerelease}";
        }

        // If no recognizable version pattern, try to extract just numbers and dots
        var numbersOnly = Regex.Match(cleaned, @"([0-9]+(?:\.[0-9]+)*)");
        if (numbersOnly.Success)
        {
            var parts = numbersOnly.Groups[1].Value.Split('.');
            var major = parts.Length > 0 ? parts[0] : "0";
            var minor = parts.Length > 1 ? parts[1] : "0";
            var patch = parts.Length > 2 ? parts[2] : "0";
            return $"{major}.{minor}.{patch}";
        }

        // Fallback to original if nothing else works
        return "Unknown";
    }
}