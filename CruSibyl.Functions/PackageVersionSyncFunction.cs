using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using CruSibyl.Core.Services;
using Microsoft.Azure.Functions.Worker.Http;
using System.Threading;
using Serilog;
using System;
using System.Net;

public class PackageVersionSyncFunction : SyncFunctionBase
{
    private readonly IConfiguration _configuration;
    private readonly IPackageVersionSyncService _service;

    public PackageVersionSyncFunction(IConfiguration configuration, IPackageVersionSyncService service)
    {
        _configuration = configuration;
        _service = service;
    }

    [Function("PackageVersionSyncFunction")]
    public async Task Run([TimerTrigger("0 0 10 * * *")] TimerInfo timer)
    {
        await ExecuteSync(() => _service.SyncPackageVersionsAsync(), "Timer trigger", "PackageVersion");
    }

    [Function("PackageVersionSyncFunction_Http")]
    public async Task<HttpResponseData> RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var wasExecuted = await ExecuteSync(() => _service.SyncPackageVersionsAsync(), "Manual trigger", "PackageVersion");

        var response = req.CreateResponse(wasExecuted ? HttpStatusCode.OK : HttpStatusCode.Conflict);
        var message = wasExecuted
            ? "Package version sync triggered successfully."
            : "Sync is already running. Please wait for completion.";

        await response.WriteStringAsync(message);
        return response;
    }
}
