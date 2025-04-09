using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace Htmx.Components.Results;

/// <summary>
/// Returns multple htmx views within a single response
/// </summary>
public class MultiSwapViewResult : IActionResult
{
    private (string PartialView, object Model)? _main;
    private readonly List<(string PartialView, object Model)> _oobs = new();

    public MultiSwapViewResult()
    { }

    protected MultiSwapViewResult(
        (string PartialView, object Model)? main = null,
        params (string PartialView, object Model)[] oobs)
    {
        _main = main;
        _oobs.AddRange(oobs);
    }

    public MultiSwapViewResult WithMainContent(string viewName, object model)
    {
        _main = (viewName, model);
        return this;
    }

    public MultiSwapViewResult WithMainContent((string viewName, object model) main)
    {
        _main = main;
        return this;
    }

    public MultiSwapViewResult WithOobContent(string viewName, object model)
    {
        _oobs.Add((viewName, model));
        return this;
    }

    public MultiSwapViewResult WithOobContent((string viewName, object model) oob)
    {
        _oobs.Add(oob);
        return this;
    }

    public MultiSwapViewResult WithOobContent(IEnumerable<(string viewName, object model)> oobList)
    {
        _oobs.AddRange(oobList);
        return this;
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.ContentType = "text/html";

        var writer = new StringWriter();

        // Render main view without OOB wrapping
        if (_main is not null)
        {
            var (mainViewName, mainModel) = _main.Value;
            string mainHtml = await RenderViewSmart(context, mainViewName, mainModel);
            writer.WriteLine(mainHtml.Trim());
        }

        // Render OOB views
        foreach (var (viewName, model) in _oobs)
        {
            string html = await RenderViewSmart(context, viewName, model);
            string wrapped = AddHxSwapToOuterElement(html.Trim());
            writer.WriteLine(wrapped);
        }

        await response.WriteAsync(writer.ToString());
    }

    private static Task<string> RenderViewSmart(ActionContext context, string viewName, object model)
    {
        return IsViewComponent(context, viewName)
            ? RenderViewComponentToString(context, viewName, model)
            : RenderPartialViewToString(context, viewName, model);
    }


    private static bool IsViewComponent(ActionContext context, string viewName)
    {
        // Try to resolve the view name to a view component
        try
        {
            var viewComponentSelector = context.HttpContext.RequestServices.GetRequiredService<IViewComponentSelector>();
            return viewComponentSelector.SelectComponent(viewName) != null;
        }
        catch
        {
            // If resolving fails, it's a regular view
            return false;
        }
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

    private static async Task<string> RenderViewComponentToString(ActionContext context, string viewComponentName, object arguments)
    {
        var httpContext = context.HttpContext;

        var viewComponentHelper = httpContext.RequestServices.GetRequiredService<IViewComponentHelper>();

        // Contextualize the helper so it knows about the current request
        if (viewComponentHelper is IViewContextAware viewContextAware)
        {
            var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = null // you can pass in a real model if needed
            };

            var tempDataProvider = httpContext.RequestServices.GetRequiredService<ITempDataProvider>();
            var tempData = new TempDataDictionary(httpContext, tempDataProvider);

            using var sw = new StringWriter();
            var viewContext = new ViewContext(context, NullView.Instance, viewData, tempData, sw, new HtmlHelperOptions());

            viewContextAware.Contextualize(viewContext);

            var content = await viewComponentHelper.InvokeAsync(viewComponentName, arguments);
            content.WriteTo(sw, HtmlEncoder.Default);
            return sw.ToString();
        }

        throw new InvalidOperationException("ViewComponentHelper does not implement IViewContextAware.");
    }
}


public class NullView : IView
{
    public static readonly NullView Instance = new();
    public string Path => "NullView";

    public Task RenderAsync(ViewContext context) => Task.CompletedTask;
}