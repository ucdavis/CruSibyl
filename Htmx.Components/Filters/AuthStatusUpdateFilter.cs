using Htmx.Components.Attributes;
using Htmx.Components.Filters;
using Htmx.Components.NavBar;
using Htmx.Components.ViewResults;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Htmx.Components.AuthStatus;

public class AuthStatusUpdateFilter : OobResultFilterBase<AuthStatusUpdateAttribute>
{
    private readonly INavProvider _navProvider;
    private readonly IAuthStatusProvider _authStatusProvider;

    public AuthStatusUpdateFilter(INavProvider navProvider, IAuthStatusProvider authStatusProvider)
    {
        _navProvider = navProvider;
        _authStatusProvider = authStatusProvider;
    }

    protected override Task<string?> GetViewNameForNonHtmxRequest(AuthStatusUpdateAttribute attribute, ControllerActionDescriptor cad)
        => throw new NotSupportedException(
            $"{nameof(AuthStatusUpdateFilter)} does not support non-HTMX requests. Use {nameof(AuthStatusViewComponent)} instead.");

    protected override async Task UpdateMultiSwapViewResultAsync(AuthStatusUpdateAttribute attribute, MultiSwapViewResult multiSwapViewResult, ResultExecutingContext context)
    {
        // Update NavBar
        var navbar = await _navProvider.BuildAsync();
        multiSwapViewResult.WithOobContent("NavBar", navbar);

        // Update AuthStatus
        var user = context.HttpContext.User;
        var authStatus = await _authStatusProvider.GetAuthStatusAsync(user);
        multiSwapViewResult.WithOobContent("AuthStatus", authStatus);
    }
}