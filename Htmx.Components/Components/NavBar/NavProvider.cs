using Htmx.Components.Action;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Htmx.Components.NavBar;

public interface INavProvider
{
    Task<IActionSet> BuildAsync(ActionContext context);
}

public class BuilderBasedNavProvider : INavProvider
{
    private readonly IActionContextAccessor _actionContextAccessor;
    private readonly Func<ActionContext, Task<ActionSetBuilder>> _builderFactory;

    public BuilderBasedNavProvider(IActionContextAccessor actionContextAccessor, Func<ActionContext, Task<ActionSetBuilder>> builderFactory)
    {
        _actionContextAccessor = actionContextAccessor;
        _builderFactory = builderFactory;
    }

    public async Task<IActionSet> BuildAsync(ActionContext context)
    {
        // Call the async factory to build the ActionSet
        var builder = await _builderFactory(context);

        // Build the final ActionSet
        return builder.Build();
    }
}