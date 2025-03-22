

using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using CruSibyl.Core.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Octokit;

namespace CruSibyl.Core.Services;

public interface IGitHubService
{
    public Task<List<ManifestData>> GetManifests(string repo);
}

public class GitHubService : IGitHubService
{
    private readonly GitHubSettings _settings;

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

        return csprojManifests.Concat(npmManifests).ToList();
    }

    Task<GitHubClient> GetGitHubClient()
    {
        // implementing as Task in case we have to do auth in the future
        return Task.FromResult(new GitHubClient(new ProductHeaderValue("YourAppName")));
    }

    static async Task<List<ManifestData>> GetCSProjManifests(GitHubClient client, string owner, string repo)
    {
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

    static async Task<List<ManifestData>> GetNPMManifests(GitHubClient client, string owner, string repo)
    {
        var packageJsonFiles = await SearchFilesInRepo(client, owner, repo, "filename:package.json");
        var manifests = new List<ManifestData>();

        foreach (var filePath in packageJsonFiles)
        {
            var jsonContent = await LoadFileContentFromRepo(client, owner, repo, filePath);
            using var doc = JsonDocument.Parse(jsonContent);
            var dependencies = ExtractNPMDependencies(doc);

            var platformVersion = doc.RootElement.TryGetProperty("engines", out var engines) &&
                                  engines.TryGetProperty("node", out var nodeVersion)
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


    static async Task<List<string>> SearchFilesInRepo(GitHubClient client, string owner, string repo, string query)
    {
        var searchQuery = $"{query} repo:{owner}/{repo}";
        var searchRequest = new SearchCodeRequest(searchQuery);
        var searchResults = await client.Search.SearchCode(searchRequest);

        return searchResults.Items.Select(item => item.Path).ToList();
    }

    static async Task<XDocument> LoadXmlFileFromRepo(GitHubClient client, string owner, string repo, string filePath)
    {
        string content = await LoadFileContentFromRepo(client, owner, repo, filePath);
        return XDocument.Parse(content);
    }

    static async Task<string> LoadFileContentFromRepo(GitHubClient client, string owner, string repo, string filePath)
    {
        var contents = await client.Repository.Content.GetAllContents(owner, repo, filePath);
        return contents.FirstOrDefault()?.Content ?? string.Empty;
    }

    static List<Dependency> ExtractCsprojDependencies(XDocument xmlDoc)
    {
        return xmlDoc.Descendants("PackageReference")
                     .Select(p => new Dependency
                     {
                         Name = p.Attribute("Include")?.Value ?? "Unknown",
                         Version = p.Attribute("Version")?.Value ?? "Unknown"
                     })
                     .ToList();
    }

    static List<Dependency> ExtractNPMDependencies(JsonDocument doc)
    {
        var packages = new List<Dependency>();
        if (doc.RootElement.TryGetProperty("dependencies", out var dependencies))
        {
            foreach (var property in dependencies.EnumerateObject())
            {
                packages.Add(new Dependency
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
                packages.Add(new Dependency
                {
                    Name = property.Name,
                    Version = property.Value.GetString() ?? "Unknown",
                    IsDevDependency = true
                });
            }
        }
        return packages;
    }
}

public class ManifestData
{
    public string Platform { get; set; } = "";
    public string PlatformVersion { get; set; } = "";
    public string Path { get; set; } = "";
    public List<Dependency> Dependencies { get; set; } = new();
}

public class Dependency
{
    public string Name { get; set; } = "Unknown";
    public string Version { get; set; } = "Unknown";
    public bool? IsDevDependency { get; set; } = null;
}