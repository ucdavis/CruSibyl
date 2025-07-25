using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using CruSibyl.Core.Services;
using Microsoft.Azure.Functions.Worker.Http;

public class PackageVersionSyncFunction
{
    private readonly IConfiguration _configuration;
    private readonly IPackageVersionSyncService _service;

    public PackageVersionSyncFunction(IConfiguration configuration, IPackageVersionSyncService service)
    {
        _configuration = configuration;
        _service = service;
    }

    [Function("PackageVersionSyncFunction")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
    {
        await _service.SyncPackageVersionsAsync();
    }

    [Function("PackageVersionSyncFunction_Http")]
    public async Task<HttpResponseData> RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        await _service.SyncPackageVersionsAsync();
        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteStringAsync("Package version sync triggered.");
        return response;
    }
}
