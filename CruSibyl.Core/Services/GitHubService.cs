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
    private DateTime? _nextAllowedCallTime = null;

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
        var csprojFiles = await SearchFilesInRepo(client, owner, repo, "extension:csproj");
        var manifests = new List<ManifestData>();

        foreach (var filePath in csprojFiles)
        {
            var xmlDoc = await LoadXmlFileFromRepo(client, owner, repo, filePath);
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
        var packageJsonFiles = await SearchFilesInRepo(client, owner, repo, "filename:package.json");
        var manifests = new List<ManifestData>();

        foreach (var filePath in packageJsonFiles)
        {
            var jsonContent = await LoadFileContentFromRepo(client, owner, repo, filePath);
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

    async Task<List<string>> SearchFilesInRepo(GitHubClient client, string owner, string repo, string query)
    {
        return await RetryApiCall(client, async () =>
        {
            await CheckNextAllowedCallTime();
            var searchQuery = $"{query} repo:{owner}/{repo}";
            var searchRequest = new SearchCodeRequest(searchQuery);
            var searchResults = await client.Search.SearchCode(searchRequest);

            return searchResults.Items.Select(item => item.Path).ToList();
        }, $"search files in {owner}/{repo}");
    }

    async Task<XDocument> LoadXmlFileFromRepo(GitHubClient client, string owner, string repo, string filePath)
    {
        return await RetryApiCall(client, async () =>
        {
            await CheckNextAllowedCallTime();
            string content = await LoadFileContentFromRepo(client, owner, repo, filePath);
            return XDocument.Parse(content);
        }, $"load XML file {filePath}");
    }

    async Task<string> LoadFileContentFromRepo(GitHubClient client, string owner, string repo, string filePath)
    {
        return await RetryApiCall(client, async () =>
        {
            await CheckNextAllowedCallTime();
            var contents = await client.Repository.Content.GetAllContents(owner, repo, filePath);
            var content = contents.FirstOrDefault()?.Content ?? "";
            // strip Zero Width No-Break Space (ZWNBSP, U+FEFF) from beginning of string
            return content.TrimStart('\uFEFF');
        }, $"load file content {filePath}");
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

    private async Task CheckNextAllowedCallTime()
    {
        await _rateLimitSemaphore.WaitAsync();
        try
        {
            // Check if we need to wait based on previous rate limit info
            if (_nextAllowedCallTime.HasValue && DateTime.UtcNow < _nextAllowedCallTime.Value)
            {
                var waitTime = _nextAllowedCallTime.Value - DateTime.UtcNow;
                Log.Information("Rate limit delay active. Waiting {WaitTime} before next request.", waitTime);
                await Task.Delay(waitTime);
            }
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    private async Task<T> RetryApiCall<T>(GitHubClient client, Func<Task<T>> apiCall, string operationName)
    {
        try
        {
            var result = await apiCall();
            
            // Update rate limit info after successful API call
            await UpdateNextAllowedCallTime(client);
            
            return result;
        }
        catch (RateLimitExceededException ex)
        {
            Log.Warning("Rate limit exceeded during {Operation}. Retrying after suggested delay.", operationName);
            var retryDelay = ex.GetRetryAfterTimeSpan();
            if (retryDelay > TimeSpan.Zero)
            {
                // Update our next allowed call time based on the exception
                await _rateLimitSemaphore.WaitAsync();
                try
                {
                    _nextAllowedCallTime = DateTime.UtcNow.Add(retryDelay);
                    Log.Information("Updated next allowed call time to {NextCallTime} based on rate limit exception", _nextAllowedCallTime);
                }
                finally
                {
                    _rateLimitSemaphore.Release();
                }

                await Task.Delay(retryDelay);
                
                // Retry once
                var result = await apiCall();
                
                // Update rate limit info after successful retry
                await UpdateNextAllowedCallTime(client);
                
                return result;
            }
            throw;
        }
    }

    private async Task UpdateNextAllowedCallTime(GitHubClient client)
    {
        await _rateLimitSemaphore.WaitAsync();
        try
        {
            var lastApiInfo = client.GetLastApiInfo();
            if (lastApiInfo?.RateLimit != null)
            {
                var rateLimit = lastApiInfo.RateLimit;
                Log.Verbose("Updated rate limit info from API response. Remaining: {Remaining}", rateLimit.Remaining);
                
                // Update next allowed call time if rate limit is low
                if (rateLimit.Remaining < 10) // Conservative threshold
                {
                    _nextAllowedCallTime = rateLimit.Reset.UtcDateTime;
                    Log.Verbose("Rate limit nearly exhausted. Next allowed call at {NextCallTime}", _nextAllowedCallTime);
                }
                else
                {
                    _nextAllowedCallTime = null; // No delay needed
                }
            }
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }
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