using Htmx.Components.State;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Htmx.Components;

public static class PageStateExtensions
{
    public static IPageState GetPageState(this HttpContext context)
    {
        if (context.Items.TryGetValue(PageStateMiddleware.HttpContextPageStateKey, out var value) && value is IPageState manager)
            return manager;
        throw new InvalidOperationException("PageState not found. Is PageStateMiddleware registered?");
    }

    public static IPageState GetPageState(this Controller controller)
        => controller.HttpContext.GetPageState();
}
