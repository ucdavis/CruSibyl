using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CruSibyl.Web.Controllers;

[Authorize]
public class AdminController : TabController
{
    public IActionResult Index() => HandleTabRequest("_Content");
}
