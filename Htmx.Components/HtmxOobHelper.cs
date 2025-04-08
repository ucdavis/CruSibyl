using Htmx.Components.NavBar;
using Htmx.Components.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace Htmx.Components;

public class HtmxOobHelper
{
    private readonly IActionContextAccessor _actionContextAccessor;
    private readonly INavProvider _navProvider;

    public HtmxOobHelper(IActionContextAccessor actionContextAccessor, INavProvider navProvider)
    {
        _actionContextAccessor = actionContextAccessor;
        _navProvider = navProvider;
    }

    public async Task<IActionResult> WithUpdatedNavbar(string contentPartialView, object contentModel, string navComponentName = "NavBar")
    {
        var context = _actionContextAccessor.ActionContext
                      ?? throw new InvalidOperationException("ActionContext is not available.");

        var nav = await _navProvider.BuildAsync(context);

        return new MultiSwapViewResult(
            (contentPartialView, contentModel),
            (navComponentName, new { Nav = nav })
        );
    }
}
