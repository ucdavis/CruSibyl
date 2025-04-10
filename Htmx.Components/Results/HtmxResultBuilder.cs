using Htmx.Components.Extensions;
using Htmx.Components.NavBar;
using Htmx.Components.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace Htmx.Components;

public class HtmxResultBuilder
{
    private readonly List<Task<(string PartialView, object Model)>> _partials = new();
    private Task<(string PartialView, object Model)>? _mainContent;

    private readonly IActionContextAccessor _actionContextAccessor;
    private readonly INavProvider _navProvider;

    public HtmxResultBuilder(IActionContextAccessor actionContextAccessor, INavProvider navProvider)
    {
        _actionContextAccessor = actionContextAccessor;
        _navProvider = navProvider;
    }

    public HtmxResultBuilder WithContent(string partialView, object model)
    {
        _mainContent = Task.FromResult((partialView, model));
        return this;
    }

    public HtmxResultBuilder WithContent(Func<Task<(string PartialView, object Model)>> contentTask)
    {
        _mainContent = contentTask();
        return this;
    }

    public HtmxResultBuilder WithOob(string partialView, object model)
    {
        _partials.Add(Task.FromResult((partialView, model)));
        return this;
    }

    public HtmxResultBuilder WithOob(Func<Task<(string PartialView, object Model)>> partialTask)
    {
        _partials.Add(partialTask());
        return this;
    }

    public HtmxResultBuilder WithOobNavbar(string navComponentName = "NavBar")
    {
        _partials.Add(GetNavbarPartial(navComponentName));
        return this;
    }

    public async Task<MultiSwapViewResult> BuildAsync()
    {
        (string viewName, object model)? main = null;

        if (_mainContent != null)
        {
            main = await _mainContent;
        }
        
        var oobs = await Task.WhenAll(_partials);
        var result = new MultiSwapViewResult()
            .WithOobContent(oobs);
        
        if (main.HasValue)
        {
            result.WithMainContent(main.Value);
        }

        return result;
    }

    private async Task<(string PartialView, object Model)> GetNavbarPartial(string navComponentName)
    {
        var nav = await _navProvider.BuildAsync(_actionContextAccessor.GetValidActionContext());
        return (navComponentName, new { Nav = nav });
    }

}