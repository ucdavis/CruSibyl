using Htmx;
using Htmx.Components;
using Htmx.Components.NavBar;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CruSibyl.Web.Controllers;

public abstract class TabController : Controller
{
    protected HtmxOobHelper HtmxOobHelper => HttpContext.RequestServices.GetRequiredService<HtmxOobHelper>();
    protected INavProvider NavProvider => HttpContext.RequestServices.GetRequiredService<INavProvider>();

    public IActionResult RenderInitialTabContent(string partialName, object model)
    {
        ViewData["InitialTabPartial"] = partialName;
        ViewData["InitialTabModel"] = model;
        return View("TabContent");
    }
}