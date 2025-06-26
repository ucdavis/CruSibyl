using Htmx;
using Htmx.Components.NavBar;
using Microsoft.AspNetCore.Mvc;

namespace CruSibyl.Web.Controllers;

public class DashboardController : Controller
{
    [NavAction(DisplayName = "Dashboard", Icon = "fas fa-tachometer-alt", Order = 0, PushUrl = true, ViewName = "_Content")]
    public IActionResult Index()
    {
        return Ok(new { });
    }
}
