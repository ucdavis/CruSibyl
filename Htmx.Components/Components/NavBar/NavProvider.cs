using Htmx.Components.Models;
using Htmx.Components.Models.Builders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Htmx.Components.NavBar;

public interface INavProvider
{
    Task<IActionSet> BuildAsync();
}


/// <summary>
/// A default INavProvider that builds it's IActionSet via a given builderFactory delegate
/// </summary> <summary>
/// 
/// </summary>
public class BuilderBasedNavProvider : INavProvider
{
    private readonly Func<ActionSetBuilder, Task> _builderFactory;
    private readonly IServiceProvider _serviceProvider;

    public BuilderBasedNavProvider(IServiceProvider serviceProvider, Func<ActionSetBuilder, Task> builderFactory)
    {
        _builderFactory = builderFactory;
        _serviceProvider = serviceProvider;
    }

    public async Task<IActionSet> BuildAsync()
    {
        // Call the async factory to build the ActionSet
        var actionSetBuilder = new ActionSetBuilder(_serviceProvider);
        await _builderFactory(actionSetBuilder);

        // Build the final ActionSet
        return await actionSetBuilder.Build();
    }
}