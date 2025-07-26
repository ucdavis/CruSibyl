using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using CruSibyl.Core.Services;
using Microsoft.Azure.Functions.Worker.Http;
using System.Threading;
using Serilog;
using System;
using System.Net;

public class ManifestSyncFunction : SyncFunctionBase
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
        await ExecuteSync(() => _service.SyncManifestsAsync(), "Timer trigger", "Manifest");
    }

    [Function("ManifestSyncFunction_Http")]
    public async Task<HttpResponseData> RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var wasExecuted = await ExecuteSync(() => _service.SyncManifestsAsync(), "Manual trigger", "Manifest");

        var response = req.CreateResponse(wasExecuted ? HttpStatusCode.OK : HttpStatusCode.Conflict);
        var message = wasExecuted
            ? "Manifest sync triggered successfully."
            : "Sync is already running. Please wait for completion.";

        await response.WriteStringAsync(message);
        return response;
    }
}
