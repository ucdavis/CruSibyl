namespace Htmx.Components.Input;

public class InputSetBuilder
{
    private readonly InputSet _set = new();

    public InputSetBuilder AddField(Action<InputFieldBuilder> configure)
    {
        var builder = new InputFieldBuilder();
        configure(builder);
        _set.Fields.Add(builder.Build());
        return this;
    }

    public InputSetBuilder AddField(InputField field)
    {
        _set.Fields.Add(field);
        return this;
    }

    public InputSetBuilder AddRange(IEnumerable<InputField> fields)
    {
        _set.Fields.AddRange(fields);
        return this;
    }

    public InputSetBuilder WithLabel(string label)
    {
        _set.Label = label;
        return this;
    }

    public InputSet Build() => _set;
}
