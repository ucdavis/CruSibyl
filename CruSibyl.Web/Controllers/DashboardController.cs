using Htmx;
using Htmx.Components.NavBar;
using Microsoft.AspNetCore.Mvc;
using CruSibyl.Web.Services;
using Microsoft.AspNetCore.Authorization;

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
