using Htmx.Components.Extensions;

namespace Htmx.Components.Models;

/// <summary>
/// Represents a single input field.
/// </summary>
public interface IInputModel
{
    string PropName { get; }
    string Id { get; }
    ModelHandler ModelHandler { get; }
    string? Label { get; }
    string? Placeholder { get; }
    string? CssClass { get; }
    InputKind Kind { get; }
    string Value { get; }
    object? ObjectValue { get; set; }
    Dictionary<string, string> Attributes { get; }
    List<KeyValuePair<string, string>>? Options { get; }
}

public class InputModel<T, TProp> : IInputModel
{
    public InputModel(InputModelConfig<T, TProp> config)
    {
        PropName = config.PropName;
        Id = config.Id;
        ModelHandler = config.ModelHandler;
        TypeId = config.TypeId;
        Label = config.Label;
        Placeholder = config.Placeholder;
        CssClass = config.CssClass;
        Kind = config.Kind;
        ObjectValue = config.ObjectValue;
        Attributes = config.Attributes;
        Options = config.Options;
    }

    public string PropName { get; set; } = "";
    public string Id { get; set; } = "";
    public ModelHandler ModelHandler { get; set; } = null!;
    public string TypeId { get; set; } = typeof(T).Name;
    public string Label { get; set; } = "";
    public string? Placeholder { get; set; }
    public string? CssClass { get; set; }
    public InputKind Kind { get; set; } = InputKind.Text;
    public string Value => ObjectValue?.ConvertToInputString() ?? string.Empty;
    public object? ObjectValue { get; set; } = null;
    public Dictionary<string, string> Attributes { get; } = new();
    public List<KeyValuePair<string, string>>? Options { get; set; }

}

/// <summary>
/// Represents a set of input fields.
/// </summary>
public class InputSet
{
    public InputSet(InputSetConfig config)
    {
        Label = config.Label;
        Inputs = config.Inputs;
    }

    public InputSet() : this(new InputSetConfig()) { }

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


public class InputModelConfig<T, TProp>
{
    public string PropName { get; set; } = "";
    public string Id { get; set; } = "";
    public ModelHandler ModelHandler { get; set; } = null!;
    public string TypeId { get; set; } = typeof(T).Name;
    public string Label { get; set; } = "";
    public string? Placeholder { get; set; }
    public string? CssClass { get; set; }
    public InputKind Kind { get; set; } = InputKind.Text;
    public object? ObjectValue { get; set; } = null;
    public Dictionary<string, string> Attributes { get; } = new();
    public List<KeyValuePair<string, string>>? Options { get; set; }
}

public class InputSetConfig
{
    public string? Label { get; set; }
    public List<IInputModel> Inputs { get; set; } = new();
}