using Htmx.Components.Models;
using Htmx.Components.Results;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Htmx.Components.State;

public class GlobalStateOobInjectorFilter : IAsyncResultFilter
{
    private readonly IGlobalStateManager _globalStateManager;

    public GlobalStateOobInjectorFilter(IGlobalStateManager globalStateManager)
    {
        _globalStateManager = globalStateManager;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is MultiSwapViewResult multiSwap && _globalStateManager.IsDirty)
        {
            var oobView = new HtmxViewInfo
            {
                ViewName = "_GlobalStateHiddenInput",
                Model = _globalStateManager.Encrypted,
                TargetRelation = OobTargetRelation.OuterHtml
            };

            multiSwap.WithOobContent(oobView);
        }

        await next();
    }
}
