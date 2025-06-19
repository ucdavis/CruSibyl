using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Htmx.Components.AuthStatus;

public class AuthStatusViewComponent : ViewComponent
{
    private readonly IAuthStatusProvider _authStatusProvider;

    public AuthStatusViewComponent(IAuthStatusProvider authStatusProvider)
    {
        _authStatusProvider = authStatusProvider;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = await _authStatusProvider.GetAuthStatusAsync(HttpContext.User);
        return View("Default", model);
    }
}