

using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Octokit;
using Serilog;

namespace CruSibyl.Core.Services;

public interface IGitHubService
{
    public Task<List<ManifestData>> GetManifests(string repo);
}

public class GitHubService : IGitHubService
{
    private readonly GitHubSettings _settings;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private int _requestCount = 0;
    private const int CoreRequestThreshold = 50;
    private const int SearchRequestThreshold = 10;

    public GitHubService(IOptions<GitHubSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task<List<ManifestData>> GetManifests(string repo)
    {
        var client = await GetGitHubClient();
        var owner = _settings.RepoOwner;

        var csprojManifests = await GetCSProjManifests(client, owner, repo);
        var npmManifests = await GetNPMManifests(client, owner, repo);

        return [.. csprojManifests, .. npmManifests];
    }

    private Task<GitHubClient> GetGitHubClient()
    {
        var gitHubClient = new GitHubClient(new ProductHeaderValue("CruSibyl"));
        gitHubClient.Credentials = new Credentials(_settings.AccessToken);
        return Task.FromResult(gitHubClient);
    }

    private async Task<List<ManifestData>> GetCSProjManifests(GitHubClient client, string owner, string repo)
    {
        Log.Information("Searching for .csproj files in repository {Repo}", repo);
        var csprojFiles = await SearchFilesInRepoAsync(client, owner, repo, "extension:csproj");
        var manifests = new List<ManifestData>();

        foreach (var filePath in csprojFiles)
        {
            var xmlDoc = await LoadXmlFileFromRepoAsync(client, owner, repo, filePath);
            var dependencies = ExtractCsprojDependencies(xmlDoc);

            var targetFramework = xmlDoc.Descendants("TargetFramework").FirstOrDefault()?.Value ?? "Unknown";
            manifests.Add(new ManifestData
            {
                Platform = "dotnet",
                PlatformVersion = targetFramework,
                Path = filePath,
                Dependencies = dependencies
            });
        }

        return manifests;
    }

    private async Task<List<ManifestData>> GetNPMManifests(GitHubClient client, string owner, string repo)
    {
        Log.Information("Searching for package.json files in repository {Repo}", repo);
        var packageJsonFiles = await SearchFilesInRepoAsync(client, owner, repo, "filename:package.json");
        var manifests = new List<ManifestData>();

        foreach (var filePath in packageJsonFiles)
        {
            var jsonContent = await LoadFileContentFromRepoAsync(client, owner, repo, filePath);
            using var doc = JsonDocument.Parse(jsonContent);
            var dependencies = ExtractNPMDependencies(doc);

            var platformVersion = doc.RootElement.TryGetProperty("engines", out var engines)
                && engines.TryGetProperty("node", out var nodeVersion)
                ? nodeVersion.GetString() ?? "Unknown"
                : "Unknown";

            manifests.Add(new ManifestData
            {
                Platform = "node",
                PlatformVersion = platformVersion,
                Path = filePath,
                Dependencies = dependencies
            });
        }

        return manifests;
    }

    async Task<List<string>> SearchFilesInRepoAsync(GitHubClient client, string owner, string repo, string query)
    {
        await ThrottleIfNeededAsync(client, GitHubRequestType.Search);
        var searchQuery = $"{query} repo:{owner}/{repo}";
        var searchRequest = new SearchCodeRequest(searchQuery);
        var searchResults = await client.Search.SearchCode(searchRequest);

        return searchResults.Items.Select(item => item.Path).ToList();
    }

    async Task<XDocument> LoadXmlFileFromRepoAsync(GitHubClient client, string owner, string repo, string filePath)
    {
        await ThrottleIfNeededAsync(client, GitHubRequestType.Core);
        string content = await LoadFileContentFromRepoAsync(client, owner, repo, filePath);
        return XDocument.Parse(content);
    }

    async Task<string> LoadFileContentFromRepoAsync(GitHubClient client, string owner, string repo, string filePath)
    {
        await ThrottleIfNeededAsync(client, GitHubRequestType.Core);
        var contents = await client.Repository.Content.GetAllContents(owner, repo, filePath);
        var content = contents.FirstOrDefault()?.Content ?? "";
        // strip Zero Width No-Break Space (ZWNBSP, U+FEFF) from beginning of string
        return content.TrimStart('\uFEFF');
    }

    static List<DependencyData> ExtractCsprojDependencies(XDocument xmlDoc)
    {
        return xmlDoc.Descendants("PackageReference")
            .Select(p => new DependencyData
            {
                Name = p.Attribute("Include")?.Value ?? "Unknown",
                Version = p.Attribute("Version")?.Value ?? "Unknown"
            })
            .ToList();
    }

    static List<DependencyData> ExtractNPMDependencies(JsonDocument doc)
    {
        var packages = new List<DependencyData>();
        if (doc.RootElement.TryGetProperty("dependencies", out var dependencies))
        {
            foreach (var property in dependencies.EnumerateObject())
            {
                packages.Add(new DependencyData
                {
                    Name = property.Name,
                    Version = property.Value.GetString() ?? "Unknown",
                    IsDevDependency = false
                });
            }
        }
        if (doc.RootElement.TryGetProperty("devDependencies", out var devDependencies))
        {
            foreach (var property in devDependencies.EnumerateObject())
            {
                packages.Add(new DependencyData
                {
                    Name = property.Name,
                    Version = property.Value.GetString() ?? "Unknown",
                    IsDevDependency = true
                });
            }
        }
        return packages;
    }

    private async Task ThrottleIfNeededAsync(GitHubClient client, GitHubRequestType type)
    {
        await _rateLimitSemaphore.WaitAsync();
        try
        {
            _requestCount++;
            int threshold = type == GitHubRequestType.Search ? SearchRequestThreshold : CoreRequestThreshold;
            if (_requestCount % threshold == 0)
            {
                var rateLimits = await client.RateLimit.GetRateLimits();
                var resource = type == GitHubRequestType.Search ? rateLimits.Resources.Search : rateLimits.Resources.Core;
                if (resource.Remaining < threshold)
                {
                    var waitTime = resource.Reset.UtcDateTime - DateTime.UtcNow;
                    if (waitTime > TimeSpan.Zero)
                    {
                        Log.Information("Rate limit reached for GitHubRequestType {RequestType}. Waiting {WaitTime} before next request.", type, waitTime);
                        await Task.Delay(waitTime);
                    }
                }
            }
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    private enum GitHubRequestType { Core, Search }

}

public class ManifestData
{
    public string Platform { get; set; } = "";
    public string PlatformVersion { get; set; } = "";
    public string Path { get; set; } = "";
    public List<DependencyData> Dependencies { get; set; } = new();
}

public class DependencyData
{
    public string Name { get; set; } = "Unknown";
    public string Version { get; set; } = "Unknown";
    public bool? IsDevDependency { get; set; } = null;
}