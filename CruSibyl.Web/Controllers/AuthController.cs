using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CruSibyl.Web.Controllers;

[Authorize]
public class AuthController : Controller
{

    [Authorize]
    [HttpGet("/auth/popup-login")]
    public IActionResult PopupLogin()
    {
        // If this executes, the user is already authenticated, so we return a view that posts
        // a "login-success" message to the parent window and closes itself.
        return View();
    }
}