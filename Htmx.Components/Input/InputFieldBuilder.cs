namespace Htmx.Components.Input;

public class InputFieldBuilder
{
    private readonly InputField _field = new();

    public InputFieldBuilder WithName(string name)
    {
        _field.Name = name;
        return this;
    }

    public InputFieldBuilder WithLabel(string label)
    {
        _field.Label = label;
        return this;
    }

    public InputFieldBuilder WithPlaceholder(string placeholder)
    {
        _field.Placeholder = placeholder;
        return this;
    }

    public InputFieldBuilder WithCssClass(string cssClass)
    {
        _field.CssClass = cssClass;
        return this;
    }

    public InputFieldBuilder WithType(string type)
    {
        _field.Type = type;
        return this;
    }

    public InputFieldBuilder WithBindingPath(string path)
    {
        _field.BindingPath = path;
        return this;
    }

    public InputFieldBuilder WithValue(string value)
    {
        _field.Value = value;
        return this;
    }

    public InputFieldBuilder WithAttribute(string key, string value)
    {
        _field.Attributes[key] = value;
        return this;
    }

    public InputField Build() => _field;
}
