using Htmx;
using Htmx.Components.NavBar;
using Microsoft.AspNetCore.Mvc;
using CruSibyl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using CruSibyl.Web.Models.Dashboard;

namespace CruSibyl.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [NavAction(DisplayName = "Dashboard", Icon = "fas fa-tachometer-alt", Order = 0, PushUrl = true, ViewName = "_Content")]
    public async Task<IActionResult> Index()
    {
        var model = await _dashboardService.GetDashboardOverviewAsync();
        return Ok(model);
    }

    [HttpGet("/Dashboard/FailureHistory")]
    public async Task<IActionResult> FailureHistory(string? app = null, string? job = null)
    {
        var model = await _dashboardService.GetFailureHistoryAsync(app, job);
        return PartialView("_FailureHistory", model);
    }

    [HttpGet("/Dashboard/Details/WebJobs")]
    public async Task<IActionResult> WebJobDetails(WebJobDetailFilter filter = WebJobDetailFilter.TotalEnabled)
    {
        var model = await _dashboardService.GetWebJobDetailsAsync(filter);
        return PartialView("_WebJobDetails", model);
    }

    [HttpGet("/Dashboard/Details/Platforms")]
    public async Task<IActionResult> PlatformDetails(PlatformDetailFilter filter = PlatformDetailFilter.WithManifests)
    {
        var model = await _dashboardService.GetPlatformDetailsAsync(filter);
        return PartialView("_PlatformDetails", model);
    }

    [HttpGet("/Dashboard/Details/Dependencies")]
    public async Task<IActionResult> DependencyDetails(DependencyDetailFilter filter = DependencyDetailFilter.All)
    {
        var model = await _dashboardService.GetDependencyDetailsAsync(filter);
        return PartialView("_DependencyCurrency", model);
    }

    [HttpGet("/Dashboard/Details/AppMappings")]
    public async Task<IActionResult> AppMappingDetails(AppMappingDetailFilter filter = AppMappingDetailFilter.Unmapped)
    {
        var model = await _dashboardService.GetAppMappingDetailsAsync(filter);
        return PartialView("_AppMappingDetails", model);
    }

    [HttpGet("/Dashboard/Details/Freshness")]
    public async Task<IActionResult> DataFreshnessDetails(DataFreshnessDetailFilter filter = DataFreshnessDetailFilter.StaleRepoScans)
    {
        var model = await _dashboardService.GetDataFreshnessDetailsAsync(filter);
        return PartialView("_DataFreshnessDetails", model);
    }

    [HttpGet("/Dashboard/WebJobDrillDown/{webJobId}")]
    public async Task<IActionResult> WebJobDrillDown(int webJobId)
    {
        var model = await _dashboardService.GetWebJobDrillDownAsync(webJobId);
        return PartialView("_WebJobDrillDown", model);
    }

    [HttpGet("/Dashboard/DependencyCurrency")]
    public async Task<IActionResult> DependencyCurrency()
    {
        var model = await _dashboardService.GetDependencyCurrencyAsync();
        return PartialView("_DependencyCurrency", model);
    }
}
