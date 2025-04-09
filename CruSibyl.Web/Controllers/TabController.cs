using Htmx;
using Htmx.Components;
using Htmx.Components.NavBar;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CruSibyl.Web.Controllers;

public abstract class TabController : Controller
{
    protected HtmxResultBuilder HtmxResultBuilder => HttpContext.RequestServices.GetRequiredService<HtmxResultBuilder>();

    public IActionResult RenderInitialTabContent(string partialName, object model)
    {
        ViewData["InitialTabPartial"] = partialName;
        ViewData["InitialTabModel"] = model;
        return View("TabContent");
    }
}