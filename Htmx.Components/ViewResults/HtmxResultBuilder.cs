using Htmx.Components.Extensions;
using Htmx.Components.Models;
using Htmx.Components.NavBar;
using Htmx.Components.ViewResults;
using Htmx.Components.State;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Htmx.Components.ViewResults;

public class HtmxResultBuilder
{
    private readonly List<Task<HtmxViewInfo>> _oobViewInfos = new();
    private Task<HtmxViewInfo>? _mainViewInfo;

    private readonly IActionContextAccessor _actionContextAccessor;
    private readonly INavProvider _navProvider;
    private readonly IPageState _pageState;

    public HtmxResultBuilder(IActionContextAccessor actionContextAccessor, INavProvider navProvider,
        IPageState pageState, IServiceProvider serviceProvider)
    {
        // Ensure the application part is registered. There's no official way to check if an extension has been added,
        // so we use a marker service to check if the extension is registered. This location seemd as good as any.
        try
        {
            serviceProvider.GetRequiredService<HtmxComponentsApplicationPartMarker>();
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"{nameof(HtmxComponentsApplicationPartMarker)} not registered. Ensure you call {nameof(ServiceCollectionExtensions.AddHtmxComponentsApplicationPart)}() during startup.");
        }

        _actionContextAccessor = actionContextAccessor;
        _navProvider = navProvider;
        _pageState = pageState;
    }

    public HtmxResultBuilder WithContent(string partialView, object model)
    {
        _mainViewInfo = Task.FromResult(new HtmxViewInfo
        {
            ViewName = partialView,
            Model = model
        });
        return this;
    }

    public HtmxResultBuilder WithContent(Func<Task<HtmxViewInfo>> contentTask)
    {
        _mainViewInfo = contentTask();
        return this;
    }

    public HtmxResultBuilder WithOob(string viewName, object model,
        OobTargetDisposition targetDisposition = OobTargetDisposition.OuterHtml, string? targetSelector = null)
    {
        _oobViewInfos.Add(Task.FromResult(new HtmxViewInfo
        {
            ViewName = viewName,
            Model = model,
            TargetDisposition = targetDisposition,
            TargetSelector = targetSelector
        }));
        return this;
    }

    public HtmxResultBuilder WithOob(string viewName, object model)
    {
        _oobViewInfos.Add(Task.FromResult(new HtmxViewInfo
        {
            ViewName = viewName,
            Model = model,
            TargetDisposition = model is IOobTargetable t1
                ? t1.TargetDisposition ?? OobTargetDisposition.OuterHtml
                : OobTargetDisposition.OuterHtml,
            TargetSelector = model is IOobTargetable t2
                ? t2.TargetSelector
                : null
        }));
        return this;
    }

    public HtmxResultBuilder WithOob(Func<Task<HtmxViewInfo>> partialTask)
    {
        _oobViewInfos.Add(partialTask());
        return this;
    }

    public HtmxResultBuilder WithOobNavbar(string navComponentName = "NavBar")
    {
        _oobViewInfos.Add(GetNavbarPartial(navComponentName));
        return this;
    }

    public async Task<MultiSwapViewResult> BuildAsync()
    {
        HtmxViewInfo? main = null;

        if (_mainViewInfo != null)
        {
            main = await _mainViewInfo;
        }

        var oobs = await Task.WhenAll(_oobViewInfos);

        var result = new MultiSwapViewResult()
            .WithOobContent(oobs);

        if (main != null)
        {
            result.WithMainContent(main.ViewName, main.Model);
        }

        return result;
    }

    private async Task<HtmxViewInfo> GetNavbarPartial(string navComponentName)
    {
        var nav = await _navProvider.BuildAsync(_actionContextAccessor.GetValidActionContext());
        return new HtmxViewInfo
        {
            ViewName = navComponentName,
            Model = nav,
            TargetDisposition = OobTargetDisposition.OuterHtml
        };
    }

}