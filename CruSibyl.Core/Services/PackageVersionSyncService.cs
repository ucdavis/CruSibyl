using CruSibyl.Core.Data;
using CruSibyl.Core.Domain;
using CruSibyl.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace CruSibyl.Core.Services;

public interface IPackageVersionSyncService
{
    Task<Result> SyncPackageVersions();
}

public class PackageVersionSyncService : IPackageVersionSyncService
{
    private readonly INuGetService _nugetService;
    private readonly INpmService _npmService;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public PackageVersionSyncService(
        INuGetService nugetService,
        INpmService npmService,
        IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _nugetService = nugetService;
        _npmService = npmService;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Result> SyncPackageVersions()
    {
        // Use a context to coordinate scan batch logic
        using var dbContext = _dbContextFactory.CreateDbContext();

        var scanInfo = await dbContext.Packages
            .GroupBy(p => 1)
            .Select(g => new
            {
                MaxScanNumber = g.Max(p => p.ScanNumber ?? 0),
                AllSameScanNumber = g.Select(p => p.ScanNumber ?? 0).Distinct().Count() == 1,
                AllCompleted = g.All(p => p.ScanStatus == ScanStatus.Completed)
            })
            .FirstAsync();

        var scanNumber = scanInfo.MaxScanNumber;
        var scanComplete = scanInfo.AllSameScanNumber && scanInfo.AllCompleted;

        Package[] packages = [];

        if (scanComplete || scanNumber == 0)
        {
            scanNumber += 1;
            packages = await dbContext.Packages.ToArrayAsync();
            foreach (var pkg in packages)
            {
                pkg.ScanNumber = scanNumber;
                pkg.ScanStatus = ScanStatus.InProgress;
            }
            await dbContext.SaveChangesAsync();
        }
        else if (!scanInfo.AllSameScanNumber)
        {
            scanNumber = Math.Max(scanNumber, 1);
            packages = await dbContext.Packages
                .Where(p => (p.ScanNumber ?? 0) != scanNumber)
                .ToArrayAsync();
            foreach (var pkg in packages)
            {
                pkg.ScanNumber = scanNumber;
                pkg.ScanStatus = ScanStatus.InProgress;
            }
            await dbContext.SaveChangesAsync();
        }

        // Only packages not complete for the current batch
        packages = await dbContext.Packages
            .Where(p => (p.ScanNumber ?? 0) == scanNumber && p.ScanStatus != ScanStatus.Completed)
            .Include(p => p.Versions)
            .Include(p => p.Platform)
            .ToArrayAsync();

        // Run NuGet and Npm sync concurrently, each with its own context
        var nugetTask = SyncRegistryPackagesAsync("dotnet", _nugetService, scanNumber);
        var npmTask = SyncRegistryPackagesAsync("node", _npmService, scanNumber);

        try
        {
            await Task.WhenAll(nugetTask, npmTask);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Error(ex.Message);
        }
    }

    private async Task SyncRegistryPackagesAsync(string platformName, IPackageRegistryService registryService, int scanNumber)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var packages = await dbContext.Packages
            .Where(p => (p.ScanNumber ?? 0) == scanNumber
                        && p.ScanStatus == ScanStatus.InProgress
                        && p.Platform.Name == platformName)
            .Include(p => p.Versions)
            .Include(p => p.Platform)
            .ToListAsync();

        foreach (var package in packages)
        {
            try
            {
                var versions = await registryService.GetLatestMinorReleasesByMajorAsync(package.Name);
                foreach (var versionInfo in versions)
                {
                    if (!package.Versions.Any(v => v.Version == versionInfo.Version))
                    {
                        package.Versions.Add(new PackageVersion
                        {
                            Package = package,
                            Version = versionInfo.Version,
                            Major = versionInfo.Major,
                            Minor = versionInfo.Minor,
                            Patch = versionInfo.Patch,
                            PreRelease = versionInfo.IsPrerelease ? "prerelease" : null
                        });
                    }
                }
                package.ScanStatus = ScanStatus.Completed;
                package.LastScannedAt = DateTime.UtcNow;
                package.ScanMessage = null;
            }
            catch (Exception ex)
            {
                package.ScanStatus = ScanStatus.Failed;
                package.ScanMessage = ex.Message;
                Log.Error(ex, "Failed to sync versions for package {Package}", package.Name);
            }
            await dbContext.SaveChangesAsync();
        }
    }
}