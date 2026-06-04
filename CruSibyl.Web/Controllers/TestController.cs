using CruSibyl.Web.Models.Test;
using Htmx.Components.NavBar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CruSibyl.Web.Controllers;

[Authorize]
[Authorize(Policy = DevelopmentOnlyPolicy)]
[Route("Test")]
public sealed class TestController : Controller
{
    private const string DevelopmentOnlyPolicy = "DevelopmentOnly";

    [HttpGet("")]
    [NavAction(DisplayName = "Test", Icon = "fas fa-vial", Order = 3, PushUrl = true, ViewName = "_ErrorTests")]
    public IActionResult Index()
    {
        return Ok(new ErrorTestViewModel());
    }
}
