using Htmx.Components.State;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Htmx.Components.TagHelpers;

[HtmlTargetElement("htmx-global-state")]
public class GlobalStateTagHelper : TagHelper
{
    private readonly IGlobalStateManager _globalStateManager;

    public GlobalStateTagHelper(IGlobalStateManager globalStateManager)
    {
        _globalStateManager = globalStateManager;
    }

    /// <summary>
    /// Optional override for the container div id. Defaults to 'global_state_container'.
    /// </summary>
    public string ContainerId { get; set; } = "global_state_container";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // We want to render a <div> containing the hidden input
        output.TagName = "div";
        output.Attributes.SetAttribute("id", ContainerId);

        var serializedState = _globalStateManager.Encrypted;

        output.Content.SetHtmlContent($@"
            <input type=""hidden"" id=""global_state"" name=""global_state"" value=""{serializedState}"">
        ");
    }
}
