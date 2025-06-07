using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Common;
using Serilog;
using System.Text.Json;
using Semver;

namespace CruSibyl.Core.Services;

public interface INpmService : IPackageRegistryService { }


public class NpmService : INpmService
{
    private static readonly string NpmRegistryUrl = "https://registry.npmjs.org/";
    private readonly SemaphoreSlim _throttleSemaphore = new(1, 1);
    private int _requestCount = 0;
    private const int RequestThreshold = 20; // Adjust as needed
    private readonly HttpClient _httpClient;

    public NpmService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<List<PackageVersionInfo>> GetLatestMinorReleasesByMajorAsync(
        string packageName,
        CancellationToken cancellationToken = default)
    {
        await ThrottleIfNeeded();

        var url = $"{NpmRegistryUrl}{packageName}";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("versions", out var versionsElement))
            return new List<PackageVersionInfo>();

        var versions = new List<(SemVersion Semver, string Version)>();

        foreach (var versionProp in versionsElement.EnumerateObject())
        {
            var versionStr = versionProp.Name;
            if (SemVersion.TryParse(versionStr, SemVersionStyles.Any, out var semver))
            {
                versions.Add((semver, versionStr));
            }
        }

        // Group by major, then select the latest minor for each major, including prerelease versions
        var latestByMajor = versions
            .GroupBy(v => v.Semver.Major)
            .Select(g => g
                .GroupBy(v => v.Semver.Minor)
                .Select(minorGroup => minorGroup
                    .OrderByDescending(v => v.Semver, SemVersion.SortOrderComparer)
                    .First())
                .OrderByDescending(v => v.Semver.Minor)
                .FirstOrDefault())
            .Where(v => !v.Equals(default((SemVersion Semver, string Version))))
            .OrderBy(v => v!.Semver.Major)
            .ToList();

        return latestByMajor
            .Select(v => new PackageVersionInfo
            {
                Major = (int)v!.Semver.Major,
                Minor = (int)v.Semver.Minor,
                Patch = (int)v.Semver.Patch,
                Version = v.Version,
                IsPrerelease = v.Semver.IsPrerelease
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
                Log.Information("Throttling npmjs API requests. Sleeping for 1 second.");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        finally
        {
            _throttleSemaphore.Release();
        }
    }
}
