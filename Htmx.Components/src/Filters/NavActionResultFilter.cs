using Htmx.Components.Attributes;
using Htmx.Components.Models;
using Htmx.Components.NavBar;
using Htmx.Components.ViewResults;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Htmx.Components.Filters;

public class NavActionResultFilter : OobResultFilterBase<NavActionAttribute>
{
    private readonly INavProvider _navProvider;
    private readonly ViewPaths _viewPaths;

    public NavActionResultFilter(INavProvider navProvider, ViewPaths viewPaths)
    {
        _navProvider = navProvider;
        _viewPaths = viewPaths;
    }

    protected override Task<string?> GetViewNameForNonHtmxRequest(NavActionAttribute attribute, ControllerActionDescriptor cad)
    {
        return Task.FromResult(attribute.ViewName);
    }

    protected override async Task UpdateMultiSwapViewResultAsync(NavActionAttribute attribute,
        MultiSwapViewResult multiSwapViewResult, ResultExecutingContext context)
    {
        if (multiSwapViewResult.Model == null)
        {
            throw new InvalidOperationException($"MultiSwapViewResult must have a model set when filtering via {nameof(NavActionAttribute)}.");
        }
        var navbar = await _navProvider.BuildAsync();

        multiSwapViewResult
            .WithOobContent(ComponentNames.NavBar, navbar)
            .WithOobContent(attribute.ViewName ?? _viewPaths.DefaultNavContent, multiSwapViewResult.Model);
    }
}