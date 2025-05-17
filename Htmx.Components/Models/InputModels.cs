namespace Htmx.Components.Models;

/// <summary>
/// Represents a single input field.
/// </summary>
public interface IInputModel
{
    string Name { get; }
    string? Label { get; }
    string? Placeholder { get; }
    string? CssClass { get; }
    InputKind Kind { get; }
    string Value { get; }
    Dictionary<string, string> Attributes { get; }
    List<KeyValuePair<string, string>>? Options { get; }
}

public class InputModel<T, TProp> : IInputModel
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Placeholder { get; set; }
    public string? CssClass { get; set; }
    public InputKind Kind { get; set; } = InputKind.Text;
    public string Value { get; set; } = "";
    public Dictionary<string, string> Attributes { get; } = new();
    public List<KeyValuePair<string, string>>? Options { get; set; }

}

/// <summary>
/// Represents a set of input fields.
/// </summary>
public class InputSet
{
    public string? Label { get; set; } = null;
    public List<IInputModel> Inputs { get; set; } = new();
}

public enum InputKind
{
    Text,
    TextArea,
    Number,
    Date,
    Checkbox,
    Radio,
    Select,
    Lookup
}