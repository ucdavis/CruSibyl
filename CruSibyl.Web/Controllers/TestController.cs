using CruSibyl.Web.Models.Test;
using CruSibyl.Web.Middleware.Auth;
using Htmx.Components.NavBar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CruSibyl.Web.Controllers;

[Authorize]
[Authorize(Policy = AuthPolicies.DevelopmentOnly)]
[Route("Test")]
public sealed class TestController : Controller
{
    [HttpGet("")]
    [NavAction(DisplayName = "Test", Icon = "fas fa-vial", Order = 3, PushUrl = true, ViewName = "_ErrorTests")]
    public IActionResult Index()
    {
        return Ok(new ErrorTestViewModel());
    }
}
