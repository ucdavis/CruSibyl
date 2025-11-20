using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    /// <summary>
    /// Get the full run history for a triggered WebJob
    /// </summary>
    Task<List<KuduLatestRun>> GetTriggeredWebJobHistory(string siteName, string jobName, string? slotName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the log output for a WebJob run from its output URL
    /// </summary>
    Task<string?> GetWebJobRunOutput(string outputUrl, CancellationToken cancellationToken = default);
}

public class KuduWebJob
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    
    [JsonPropertyName("type")]
    public string? JobType { get; set; }
    
    [JsonPropertyName("run_command")]
    public string? RunCommand { get; set; }
    
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    
    [JsonPropertyName("extra_info_url")]
    public string? ExtraInfoUrl { get; set; }
    
    [JsonPropertyName("latest_run")]
    public KuduLatestRun? LatestRun { get; set; }
}

public class KuduLatestRun
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("status")]
    public string? Status { get; set; }
    
    [JsonPropertyName("start_time")]
    public DateTime? StartTime { get; set; }
    
    [JsonPropertyName("end_time")]
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Duration as TimeSpan string (e.g., "00:01:39.6672526")
    /// </summary>
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }
    
    /// <summary>
    /// Get duration in milliseconds
    /// </summary>
    public long? GetDurationMs()
    {
        if (string.IsNullOrEmpty(Duration))
            return null;
            
        if (TimeSpan.TryParse(Duration, out var timespan))
            return (long)timespan.TotalMilliseconds;
            
        return null;
    }
    
    [JsonPropertyName("output_url")]
    public string? OutputUrl { get; set; }
    
    [JsonPropertyName("error_url")]
    public string? ErrorUrl { get; set; }
    
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class KuduWebJobHistoryResponse
{
    [JsonPropertyName("runs")]
    public List<KuduLatestRun>? Runs { get; set; }
}

public class KuduTriggeredWebJobDetails
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("run_command")]
    public string? RunCommand { get; set; }
    
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    
    [JsonPropertyName("extra_info_url")]
    public string? ExtraInfoUrl { get; set; }
    
    [JsonPropertyName("history_url")]
    public string? HistoryUrl { get; set; }
    
    [JsonPropertyName("scheduler_logs_url")]
    public string? SchedulerLogsUrl { get; set; }
    
    [JsonPropertyName("latest_run")]
    public KuduLatestRun? LatestRun { get; set; }
    
    /// <summary>
    /// Full run history list - populated when fetching history, not from detail endpoint
    /// </summary>
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
        Log.Debug("Fetching continuous WebJobs from {Url}", url);
        return await GetWebJobs(url, cancellationToken);
    }

    public async Task<List<KuduWebJob>> GetTriggeredWebJobs(string siteName, string? slotName = null, CancellationToken cancellationToken = default)
    {
        var url = BuildKuduUrl(siteName, "api/triggeredwebjobs", slotName);
        Log.Debug("Fetching triggered WebJobs from {Url}", url);
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

    public async Task<List<KuduLatestRun>> GetTriggeredWebJobHistory(string siteName, string jobName, string? slotName = null, CancellationToken cancellationToken = default)
    {
        var url = BuildKuduUrl(siteName, $"api/triggeredwebjobs/{jobName}/history", slotName);
        
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            await AuthenticateRequest(request, cancellationToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            Log.Debug("Kudu API history response from {Url}: {Json}", url, json);
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var historyResponse = JsonSerializer.Deserialize<KuduWebJobHistoryResponse>(json, options);
            var history = historyResponse?.Runs ?? new List<KuduLatestRun>();
            
            Log.Debug("Deserialized {Count} history runs for {JobName}", history.Count, jobName);

            return history;
        }
        catch (HttpRequestException ex)
        {
            // 404 is normal if no run history exists
            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Log.Debug("No run history found for {JobName} at {Url} (404)", jobName, url);
                return new List<KuduLatestRun>();
            }
            
            Log.Error(ex, "Failed to get WebJob history for {JobName} from {SiteName}", jobName, siteName);
            return new List<KuduLatestRun>();
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Failed to parse WebJob history response for {JobName} from {SiteName}", jobName, siteName);
            return new List<KuduLatestRun>();
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
            Log.Debug("Kudu API response from {Url}: {Json}", url, json);
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var webJobs = JsonSerializer.Deserialize<List<KuduWebJob>>(json, options) ?? new List<KuduWebJob>();
            
            Log.Debug("Deserialized {Count} WebJobs, names: {Names}", 
                webJobs.Count, 
                string.Join(", ", webJobs.Select(w => w.Name ?? "null")));

            return webJobs;
        }
        catch (HttpRequestException ex)
        {
            // 404 is normal if no WebJobs of this type exist
            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Log.Debug("No WebJobs found at {Url} (404)", url);
                return new List<KuduWebJob>();
            }
            
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

    public async Task<string?> GetWebJobRunOutput(string outputUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, outputUrl);
            await AuthenticateRequest(request, cancellationToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Truncate if too large (max 50KB)
            const int maxLength = 50 * 1024;
            if (content.Length > maxLength)
            {
                Log.Warning("WebJob run output truncated from {OriginalLength} to {MaxLength} bytes", content.Length, maxLength);
                return content.Substring(0, maxLength) + "\n\n[... Output truncated ...]";
            }

            return content;
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Failed to get WebJob run output from {OutputUrl}", outputUrl);
            return null;
        }
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
