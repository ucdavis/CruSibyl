using Htmx.Components.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Htmx.Components.NavBar;

public class NavBarViewComponent : ViewComponent
{
    private readonly INavProvider _navProvider;
    private readonly ViewPaths _viewPaths;

    public NavBarViewComponent(INavProvider navProvider, ViewPaths viewPaths)
    {
        _navProvider = navProvider;
        _viewPaths = viewPaths;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = await _navProvider.BuildAsync();
        return View(_viewPaths.NavBar, model);
    }
}