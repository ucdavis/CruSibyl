using Microsoft.AspNetCore.Mvc;

namespace CruSibyl.Web.Controllers;

public class DashboardController : TabController
{
    public IActionResult Index() => HandleTabRequest("_Content");
}
