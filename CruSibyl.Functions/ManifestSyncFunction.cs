using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using CruSibyl.Core.Services;

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
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
    {
        await _service.SyncManifestsAsync();
    }
}
