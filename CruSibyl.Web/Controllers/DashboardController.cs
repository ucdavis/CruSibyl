using Htmx;
using Htmx.Components.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace CruSibyl.Web.Controllers;

public class DashboardController : TabController
{
    [NavAction(DisplayName = "Dashboard", Icon = "fas fa-tachometer-alt", Order = 0, PushUrl = true)]
    public async Task<IActionResult> Index()
    {
        if (Request.IsHtmx())
        {
            return await HtmxResultBuilder
                .WithOobNavbar()
                .WithOob("_Content", new { })
                .BuildAsync();
        }

        return RenderInitialMainContent("_Content", new { });
    }
}
