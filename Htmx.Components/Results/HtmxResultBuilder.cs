using Htmx.Components.Extensions;
using Htmx.Components.Models;
using Htmx.Components.NavBar;
using Htmx.Components.Results;
using Htmx.Components.State;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace Htmx.Components;

public class HtmxResultBuilder
{
    private readonly List<Task<HtmxViewInfo>> _oobViewInfos = new();
    private Task<HtmxViewInfo>? _mainViewInfo;

    private readonly IActionContextAccessor _actionContextAccessor;
    private readonly INavProvider _navProvider;
    private readonly IGlobalStateManager _globalStateManager;

    public HtmxResultBuilder(IActionContextAccessor actionContextAccessor, INavProvider navProvider,
        IGlobalStateManager globalStateManager)
    {
        _actionContextAccessor = actionContextAccessor;
        _navProvider = navProvider;
        _globalStateManager = globalStateManager;
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
        OobTargetRelation targetRelation = OobTargetRelation.OuterHtml, string? targetSelector = null)
    {
        _oobViewInfos.Add(Task.FromResult(new HtmxViewInfo
        {
            ViewName = viewName,
            Model = model,
            TargetRelation = targetRelation,
            TargetSelector = targetSelector
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
            TargetRelation = OobTargetRelation.OuterHtml
        };
    }

}