using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using CruSibyl.Core.Services;
using Microsoft.Azure.Functions.Worker.Http;

public class ManifestSyncFunction
{
    private readonly IConfiguration _configuration;
    private readonly IManifestSyncService _service;

    public ManifestSyncFunction(IConfiguration configuration, IManifestSyncService service)
    {
        _configuration = configuration;
        _service = service;
    }

    [Function("ManifestSyncFunction")]
    public async Task Run([TimerTrigger("0 0 8 * * *")] TimerInfo timer)
    {
        await _service.SyncManifestsAsync();
    }

    [Function("ManifestSyncFunction_Http")]
    public async Task<HttpResponseData> RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        await _service.SyncManifestsAsync();
        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteStringAsync("Manifest sync triggered.");
        return response;
    }
}
