using System.Linq.Expressions;
using Htmx.Components.Extensions;
using Htmx.Components.Models;
using Humanizer;

namespace Htmx.Components.Models.Builders;

public interface IInputModelBuilder
{
    IInputModel Build();
}

public class InputModelBuilder<T, TProp> : IInputModelBuilder
    where T : class
{
    private readonly InputModel<T, TProp> _inputModel = new();

    internal InputModelBuilder(Expression<Func<T, TProp>> propertySelector)
    {
        var propName = propertySelector.GetPropertyName();
        _inputModel.Name = propName;
        _inputModel.Kind = GetInputKind(typeof(TProp));
        _inputModel.Label = propName.Humanize();
    }

    public InputModelBuilder<T, TProp> WithKind(InputKind kind)
    {
        _inputModel.Kind = kind;
        return this;
    }

    public InputModelBuilder<T, TProp> WithName(string name)
    {
        _inputModel.Name = name;
        return this;
    }

    public InputModelBuilder<T, TProp> WithLabel(string label)
    {
        _inputModel.Label = label;
        return this;
    }

    public InputModelBuilder<T, TProp> WithPlaceholder(string placeholder)
    {
        _inputModel.Placeholder = placeholder;
        return this;
    }

    public InputModelBuilder<T, TProp> WithCssClass(string cssClass)
    {
        _inputModel.CssClass = cssClass;
        return this;
    }

    public InputModelBuilder<T, TProp> WithValue(string value)
    {
        _inputModel.Value = value;
        return this;
    }

    public InputModelBuilder<T, TProp> WithAttribute(string key, string value)
    {
        _inputModel.Attributes[key] = value;
        return this;
    }

    public InputModelBuilder<T, TProp> WithOptions(IEnumerable<KeyValuePair<string, string>> options)
    {
        _inputModel.Options = options.ToList();
        return this;
    }

    IInputModel IInputModelBuilder.Build() => Build();

    public InputModel<T, TProp> Build() => _inputModel;

    private static InputKind GetInputKind(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType switch
        {
            Type t when t == typeof(string) => InputKind.Text,
            Type t when t == typeof(DateTime) => InputKind.Date,
            Type t when t == typeof(int) || t == typeof(long) || t == typeof(float) || t == typeof(double) => InputKind.Number,
            Type t when t == typeof(bool) => InputKind.Checkbox,
            Type t when t.IsEnum => InputKind.Radio,
            _ => InputKind.Text
        };
    }

}
