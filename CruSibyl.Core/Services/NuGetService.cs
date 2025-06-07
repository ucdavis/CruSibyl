using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Common;
using Serilog;

namespace CruSibyl.Core.Services;

public interface INuGetService : IPackageRegistryService { }

public class NuGetService : INuGetService
{
    private static readonly string NuGetV3Url = "https://api.nuget.org/v3/index.json";
    private readonly SemaphoreSlim _throttleSemaphore = new(1, 1);
    private int _requestCount = 0;
    private const int RequestThreshold = 20; // Adjust as needed

public async Task<List<PackageVersionInfo>> GetLatestMinorReleasesByMajorAsync(
    string packageName,
    CancellationToken cancellationToken = default)
{
    await ThrottleIfNeeded();

    var cache = new SourceCacheContext();
    var repository = Repository.Factory.GetCoreV3(NuGetV3Url);
    var resource = await repository.GetResourceAsync<FindPackageByIdResource>();

    var versions = await resource.GetAllVersionsAsync(
        packageName,
        cache,
        NullLogger.Instance,
        cancellationToken);

    // Group by major, then select the latest minor for each major, including prerelease versions
    var latestByMajor = versions
        .GroupBy(v => v.Major)
        .Select(g => g
            .GroupBy(v => v.Minor)
            .Select(minorGroup => minorGroup.Max())
            .OrderByDescending(v => v)
            .FirstOrDefault())
        .Where(v => v != null)
        .OrderBy(v => v!.Major)
        .ToList();

    return latestByMajor
        .Where(v => v != null)
        .Select(v => new PackageVersionInfo
        {
            Major = v!.Major,
            Minor = v.Minor,
            Patch = v.Patch,
            Version = v.ToNormalizedString(),
            IsPrerelease = v.IsPrerelease
        })
        .ToList();
}

    private async Task ThrottleIfNeeded()
    {
        await _throttleSemaphore.WaitAsync();
        try
        {
            _requestCount++;
            if (_requestCount % RequestThreshold == 0)
            {
                // NuGet API is generous, but you may want to add a delay if you see throttling responses.
                Log.Information("Throttling NuGet API requests. Sleeping for 1 second.");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        finally
        {
            _throttleSemaphore.Release();
        }
    }
}

