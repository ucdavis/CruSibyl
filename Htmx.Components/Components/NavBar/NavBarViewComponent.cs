using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Htmx.Components.NavBar;

public class NavBarViewComponent : ViewComponent
{
    private readonly INavProvider _navProvider;

    public NavBarViewComponent(INavProvider navProvider)
    {
        _navProvider = navProvider;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = await _navProvider.BuildAsync();
        return View("Default", model);
    }
}