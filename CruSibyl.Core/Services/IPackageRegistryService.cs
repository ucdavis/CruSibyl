namespace CruSibyl.Core.Services;

/// <summary>
/// Common interface for <seealso cref="INpmService"/> and <seealso cref="INuGetService"/>.
/// </summary>
public interface IPackageRegistryService
{
    Task<List<PackageVersionInfo>> GetLatestMinorReleasesByMajorAsync(string packageName, CancellationToken cancellationToken = default);
}

public class PackageVersionInfo
{
    public int? Major { get; set; }
    public int? Minor { get; set; }
    public int? Patch { get; set; }
    public string Version { get; set; } = "";
    public bool IsPrerelease { get; set; }
}