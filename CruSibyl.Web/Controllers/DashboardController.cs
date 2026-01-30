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
    public async Task<IActionResult> Index(string? subscriptionId = null)
    {
        subscriptionId ??= _dashboardService.GetDefaultSubscriptionId();
        var model = await _dashboardService.GetDashboardOverviewAsync(subscriptionId);
        return Ok(model);
    }

    [HttpGet("/Dashboard/FailureHistory")]
    public async Task<IActionResult> FailureHistory(string? subscriptionId = null, string? app = null, string? job = null)
    {
        subscriptionId ??= _dashboardService.GetDefaultSubscriptionId();
        var model = await _dashboardService.GetFailureHistoryAsync(subscriptionId, app, job);
        return PartialView("_FailureHistory", model);
    }

    [HttpGet("/Dashboard/WebJobDrillDown/{webJobId}")]
    public async Task<IActionResult> WebJobDrillDown(int webJobId)
    {
        var model = await _dashboardService.GetWebJobDrillDownAsync(webJobId);
        return PartialView("_WebJobDrillDown", model);
    }

    [HttpGet("/Dashboard/DependencyCurrency")]
    public async Task<IActionResult> DependencyCurrency(string? subscriptionId = null)
    {
        subscriptionId ??= _dashboardService.GetDefaultSubscriptionId();
        var model = await _dashboardService.GetDependencyCurrencyAsync(subscriptionId);
        return PartialView("_DependencyCurrency", model);
    }
}
