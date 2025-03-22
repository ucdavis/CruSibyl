using Microsoft.AspNetCore.Mvc;

namespace CruSibyl.Web.Controllers;

public class AdminController : TabController
{
    public IActionResult Index() => HandleTabRequest("_Content");
}
