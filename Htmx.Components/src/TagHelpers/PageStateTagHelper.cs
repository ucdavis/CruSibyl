using Htmx.Components.State;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Htmx.Components.TagHelpers;

[HtmlTargetElement("htmx-page-state")]
public class PageStateTagHelper : TagHelper
{
    private readonly IPageState _pageState;

    public PageStateTagHelper(IPageState pageState)
    {
        _pageState = pageState;
    }

    /// <summary>
    /// Optional override for the container div id. Defaults to 'page_state_container'.
    /// </summary>
    public string ContainerId { get; set; } = "page_state_container";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // We want to render a <div> containing the hidden input
        output.TagName = "div";
        output.Attributes.SetAttribute("id", ContainerId);

        var serializedState = _pageState.Encrypted;

        output.Content.SetHtmlContent($@"
            <input type=""hidden"" id=""page_state"" name=""page_state"" value=""{serializedState}"">
        ");
    }
}
