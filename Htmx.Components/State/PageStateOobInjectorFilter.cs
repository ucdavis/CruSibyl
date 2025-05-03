using Htmx.Components.Models;
using Htmx.Components.Results;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Htmx.Components.State;

public class PageStateOobInjectorFilter : IAsyncResultFilter
{
    private readonly IPageState _pageState;

    public PageStateOobInjectorFilter(IPageState pageState)
    {
        _pageState = pageState;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is MultiSwapViewResult multiSwap && _pageState.IsDirty)
        {
            var oobView = new HtmxViewInfo
            {
                ViewName = "_PageStateHiddenInput",
                Model = _pageState.Encrypted,
                TargetRelation = OobTargetRelation.OuterHtml
            };

            multiSwap.WithOobContent(oobView);
        }

        await next();
    }
}
