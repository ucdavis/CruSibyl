using Htmx;
using Htmx.Components;
using Htmx.Components.NavBar;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CruSibyl.Web.Controllers;

public abstract class TabController : Controller
{
    protected HtmxResultBuilder HtmxResultBuilder => HttpContext.RequestServices.GetRequiredService<HtmxResultBuilder>();

    public IActionResult RenderInitialMainContent(string partialName, object model)
    {
        ViewData["InitialMainPartial"] = partialName;
        ViewData["InitialMainModel"] = model;
        return View("MainContent");
    }
}