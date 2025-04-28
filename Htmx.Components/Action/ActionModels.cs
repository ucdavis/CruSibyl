namespace Htmx.Components.Action;

/// <summary>
/// A visual item that has a label and optional Icon and css properties
/// </summary>
public interface IActionItem
{
    string Label { get; set; }
    string? Icon { get; set; }
    string? CssClass { get; set; }
}

/// <summary>
/// A plain container for a group of IActionItem elements, without any display properties.
/// Useful for menus, toolbars, or navbars where no label/icon is needed.
/// </summary>
public interface IActionSet
{
    public List<IActionItem> Items { get; set; }
}

/// <summary>
/// A visual item that has a label and optional Icon and css properties
/// </summary>
public class ActionItem: IActionItem
{
    public string Label { get; set; } = "";
    public string? Icon { get; set; }
    public string? CssClass { get; set; }
}


/// <summary>
/// A plain container for a group of IActionItem elements, without any display properties.
/// Useful for menus, toolbars, or navbars where no label/icon is needed.
/// </summary>
public class ActionSet : IActionSet
{
    public List<IActionItem> Items { get; set; } = new();
}

/// <summary>
/// An ActionItem that contains nested IActionItems
/// </summary>
public class ActionGroup : ActionItem, IActionSet
{
    public List<IActionItem> Items { get; set; } = new();
}

/// <summary>
/// An ActionItem that takes additional attributes, primarily for adding hx-attributes
/// </summary>
public class ActionModel : ActionItem
{
    public Dictionary<string, string> Attributes { get; } = new();

    public ActionModel() { }
    
    public ActionModel(string label)
    {
        Label = label;
    }
    
    public ActionModel WithLabel(string label)
    {
        Label = label;
        return this;
    }

    public ActionModel WithIcon(string icon)
    {
        Icon = icon;
        return this;
    }

    public ActionModel WithClass(string cssClass)
    {
        CssClass = cssClass;
        return this;
    }

    public ActionModel WithAttribute(string name, string value)
    {
        Attributes[name] = value;
        return this;
    }

    public ActionModel WithHxGet(string url) => WithAttribute("hx-get", url);
    public ActionModel WithHxPost(string url) => WithAttribute("hx-post", url);
    public ActionModel WithHxTarget(string target) => WithAttribute("hx-target", target);
    public ActionModel WithHxSwap(string swap) => WithAttribute("hx-swap", swap);
    public ActionModel WithHxPushUrl(string pushUrl = "true") => WithAttribute("hx-push-url", pushUrl);
    public ActionModel WithHxInclude(string selector) => WithAttribute("hx-include", selector);
}
