namespace Htmx.Components.Models;

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
    public List<IActionItem> Items { get; set; }

    public ActionSet(ActionSetConfig config)
    {
        Items = config.Items;
    }

    public ActionSet() : this(new ActionSetConfig()) { }
}

/// <summary>
/// An ActionItem that contains nested IActionItems
/// </summary>
public class ActionGroup : ActionItem, IActionSet
{
    public List<IActionItem> Items { get; set; }

    public ActionGroup(ActionGroupConfig config)
    {
        Label = config.Label;
        Icon = config.Icon;
        CssClass = config.CssClass;
        Items = config.Items;
    }

    public ActionGroup() : this(new ActionGroupConfig()) { }
}

/// <summary>
/// An ActionItem that takes additional attributes, primarily for adding hx-attributes
/// </summary>
public class ActionModel : ActionItem
{
    public Dictionary<string, string> Attributes { get; init; } = new();

    public ActionModel(ActionModelConfig config)
    {
        Label = config.Label;
        Icon = config.Icon;
        CssClass = config.CssClass;
        Attributes = new Dictionary<string, string>(config.Attributes);
    }
}

public class ActionModelConfig
{
    public string Label { get; set; } = "";
    public string? Icon { get; set; }
    public string? CssClass { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
}

public class ActionSetConfig
{
    public List<IActionItem> Items { get; set; } = new();
}

public class ActionGroupConfig : ActionSetConfig
{
    public string Label { get; set; } = "";
    public string? Icon { get; set; }
    public string? CssClass { get; set; }
}