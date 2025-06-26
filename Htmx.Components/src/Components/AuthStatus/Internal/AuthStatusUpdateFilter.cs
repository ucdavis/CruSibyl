using Htmx.Components.AuthStatus;
using Htmx.Components.Filters;
using Htmx.Components.Models;
using Htmx.Components.NavBar;
using Htmx.Components.ViewResults;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Htmx.Components.AuthStatus.Internal;

public class AuthStatusUpdateFilter : OobResultFilterBase<AuthStatusUpdateAttribute>
{
    private readonly INavProvider _navProvider;
    private readonly IAuthStatusProvider _authStatusProvider;
    private readonly ViewPaths _viewPaths;

    public AuthStatusUpdateFilter(INavProvider navProvider, IAuthStatusProvider authStatusProvider, ViewPaths viewPaths)
    {
        _navProvider = navProvider;
        _authStatusProvider = authStatusProvider;
        _viewPaths = viewPaths;
    }

    protected override Task<string?> GetViewNameForNonHtmxRequest(AuthStatusUpdateAttribute attribute, ControllerActionDescriptor cad)
        => throw new NotSupportedException(
            $"{nameof(AuthStatusUpdateFilter)} does not support non-HTMX requests. Use {nameof(AuthStatusViewComponent)} instead.");

    protected override async Task UpdateMultiSwapViewResultAsync(AuthStatusUpdateAttribute attribute, MultiSwapViewResult multiSwapViewResult, ResultExecutingContext context)
    {
        // Update NavBar
        var navbar = await _navProvider.BuildAsync();
        multiSwapViewResult.WithOobContent(ComponentNames.NavBar, navbar);

        // Update AuthStatus
        var user = context.HttpContext.User;
        var authStatus = await _authStatusProvider.GetAuthStatusAsync(user);
        multiSwapViewResult.WithOobContent(ComponentNames.AuthStatus, authStatus);
    }
}