namespace Htmx.Components.Input;

/// <summary>
/// Represents a single input field.
/// </summary>
public class InputField
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Placeholder { get; set; }
    public string? CssClass { get; set; }
    public string? Type { get; set; }
    public string? BindingPath { get; set; }
    public string? Value { get; set; }
    public Dictionary<string, string> Attributes { get; } = new();
}

/// <summary>
/// Represents a set of input fields.
/// </summary>
public class InputSet
{
    public string? Label { get; set; } = null;
    public List<InputField> Fields { get; set; } = new();
}
