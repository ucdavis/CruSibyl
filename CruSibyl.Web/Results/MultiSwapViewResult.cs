using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace CruSibyl.Web.Results;

/// <summary>
/// Returns multple htmx views within a single response
/// </summary>
/// <remarks>
/// Requires the htmx multi-swap extension. <see cref="https://v1.htmx.org/extensions/multi-swap/"/>
/// </remarks>
public class MultiSwapViewResult : IActionResult
{
    private readonly (string PartialView, object Model)[] _views;

    public MultiSwapViewResult(params (string PartialView, object Model)[] views)
    {
        _views = views;
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.ContentType = "text/html";

        var writer = new StringWriter();
        foreach (var (partialView, model) in _views)
        {
            var content = await RenderPartialViewToString(context, partialView, model);
            writer.Write(AddHxSwapToOuterElement(content.Trim()));
        }

        await response.WriteAsync(writer.ToString());
    }

    private static string AddHxSwapToOuterElement(string html)
    {
        // Use a regex to identify the outermost tag and add hx-swap-oob="true" to it
        var regex = new Regex(@"<(\w+)([^>]*)>");
        var match = regex.Match(html);

        if (match.Success)
        {
            // Check if the outermost tag already contains hx-swap-oob
            if (!match.Value.Contains("hx-swap-oob"))
            {
                var tagName = match.Groups[1].Value;
                var tagAttributes = match.Groups[2].Value;

                // Add the hx-swap-oob attribute to the outermost element's tag
                var updatedTag = $"<{tagName}{tagAttributes} hx-swap-oob=\"outerHTML\">";

                // Replace the opening tag with the updated one
                return $"<template>{regex.Replace(html, updatedTag, 1)}</template>";
                //return regex.Replace(html, updatedTag, 1);
            }
        }

        return html;
    }

    private static async Task<string> RenderPartialViewToString(ActionContext context, string viewName, object model)
    {
        var httpContext = context.HttpContext;
        var controller = context.RouteData.Values["controller"]?.ToString();
        var viewEngine = httpContext.RequestServices.GetService<ICompositeViewEngine>()!;
        var tempDataProvider = httpContext.RequestServices.GetService<ITempDataProvider>()!;

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model
        };

        using var sw = new StringWriter();
        var viewResult = viewEngine.FindView(context, viewName, false);
        if (viewResult.View == null)
        {
            throw new InvalidOperationException($"The partial view '{viewName}' was not found. Searched locations: {string.Join(", ", viewResult.SearchedLocations ?? Enumerable.Empty<string>())}");
        }
        var tempData = new TempDataDictionary(httpContext, tempDataProvider);
        var viewContext = new ViewContext(context, viewResult.View, viewData, tempData, sw, new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return sw.ToString();
    }

}
