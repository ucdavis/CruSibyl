using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Serilog;

namespace CruSibyl.Core.Services;

/// <summary>
/// Service for interacting with Kudu (SCM) REST API to manage WebJobs
/// Kudu API documentation: https://github.com/projectkudu/kudu/wiki/REST-API
/// </summary>
public interface IKuduApiService
{
    /// <summary>
    /// Get all continuous WebJobs for an app
    /// </summary>
    Task<List<KuduWebJob>> GetContinuousWebJobs(string siteName, string? slotName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all triggered WebJobs for an app
    /// </summary>
    Task<List<KuduWebJob>> GetTriggeredWebJobs(string siteName, string? slotName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get details and run history for a specific triggered WebJob
    /// </summary>
    Task<KuduTriggeredWebJobDetails?> GetTriggeredWebJobDetails(string siteName, string jobName, string? slotName = null, CancellationToken cancellationToken = default);
}

public class KuduWebJob
{
    public string? Name { get; set; }
    public string? Status { get; set; }
    public string? JobType { get; set; }
    public string? RunCommand { get; set; }
    public string? Url { get; set; }
    public string? ExtraInfoUrl { get; set; }
    public KuduLatestRun? LatestRun { get; set; }
}

public class KuduLatestRun
{
    public string? Id { get; set; }
    public string? Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long? Duration { get; set; }
    public string? OutputUrl { get; set; }
    public string? ErrorUrl { get; set; }
    public string? Url { get; set; }
}

public class KuduTriggeredWebJobDetails
{
    public string? Name { get; set; }
    public string? RunCommand { get; set; }
    public string? Url { get; set; }
    public string? ExtraInfoUrl { get; set; }
    public string? HistoryUrl { get; set; }
    public string? SchedulerLogsUrl { get; set; }
    public List<KuduLatestRun>? LatestRuns { get; set; }
}

public class KuduApiService : IKuduApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;

    public KuduApiService(
        HttpClient httpClient,
        TokenCredential? credential = null)
    {
        _httpClient = httpClient;
        _credential = credential ?? new DefaultAzureCredential();
    }

    public async Task<List<KuduWebJob>> GetContinuousWebJobs(string siteName, string? slotName = null, CancellationToken cancellationToken = default)
    {
        var url = BuildKuduUrl(siteName, "api/continuouswebjobs", slotName);
        return await GetWebJobs(url, cancellationToken);
    }

    public async Task<List<KuduWebJob>> GetTriggeredWebJobs(string siteName, string? slotName = null, CancellationToken cancellationToken = default)
    {
        var url = BuildKuduUrl(siteName, "api/triggeredwebjobs", slotName);
        return await GetWebJobs(url, cancellationToken);
    }

    public async Task<KuduTriggeredWebJobDetails?> GetTriggeredWebJobDetails(string siteName, string jobName, string? slotName = null, CancellationToken cancellationToken = default)
    {
        var url = BuildKuduUrl(siteName, $"api/triggeredwebjobs/{jobName}", slotName);
        
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            await AuthenticateRequest(request, cancellationToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var details = JsonSerializer.Deserialize<KuduTriggeredWebJobDetails>(json, options);

            return JsonSerializer.Deserialize<KuduTriggeredWebJobDetails>(json);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Failed to get triggered WebJob details for {JobName} from {SiteName}", jobName, siteName);
            return null;
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Failed to parse triggered WebJob details response for {JobName} from {SiteName}", jobName, siteName);
            return null;
        }
    }

    private async Task<List<KuduWebJob>> GetWebJobs(string url, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            await AuthenticateRequest(request, cancellationToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var webJobs = JsonSerializer.Deserialize<List<KuduWebJob>>(json, options);

            return JsonSerializer.Deserialize<List<KuduWebJob>>(json) ?? new List<KuduWebJob>();
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Failed to get WebJobs from {Url}", url);
            return new List<KuduWebJob>();
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Failed to parse WebJobs response from {Url}", url);
            return new List<KuduWebJob>();
        }
    }

    private async Task AuthenticateRequest(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Get Azure token for App Service management
        var tokenContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
        var token = await _credential.GetTokenAsync(tokenContext, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private static string BuildKuduUrl(string siteName, string path, string? slotName = null)
    {
        // Kudu URL format: https://{sitename}.scm.azurewebsites.net/{path}
        // For slots: https://{sitename}-{slotname}.scm.azurewebsites.net/{path}
        var scmSite = string.IsNullOrEmpty(slotName) 
            ? $"{siteName}.scm.azurewebsites.net" 
            : $"{siteName}-{slotName}.scm.azurewebsites.net";
        
        return $"https://{scmSite}/{path}";
    }
}
