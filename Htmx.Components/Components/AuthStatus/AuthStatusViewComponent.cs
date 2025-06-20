using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Htmx.Components.Models;

namespace Htmx.Components.AuthStatus;

public class AuthStatusViewComponent : ViewComponent
{
    private readonly IAuthStatusProvider _authStatusProvider;
    private readonly ViewPaths _viewPaths;

    public AuthStatusViewComponent(IAuthStatusProvider authStatusProvider, ViewPaths view)
    {
        _authStatusProvider = authStatusProvider;
        _viewPaths = view;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = await _authStatusProvider.GetAuthStatusAsync(HttpContext.User);
        return View(_viewPaths.AuthStatus, model);
    }
}