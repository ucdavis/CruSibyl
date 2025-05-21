using System.Linq.Expressions;
using Htmx.Components.Extensions;
using Htmx.Components.Models;
using Humanizer;

namespace Htmx.Components.Models.Builders;

public interface IInputModelBuilder
{
    Task<IInputModel> Build();
}

public class InputModelBuilder<T, TProp> : BuilderBase<InputModelBuilder<T, TProp>, InputModel<T, TProp>>, IInputModelBuilder
    where T : class
{
    internal InputModelBuilder(IServiceProvider serviceProvider, Expression<Func<T, TProp>> propertySelector) 
        : base(serviceProvider)
    {
        var propName = propertySelector.GetPropertyName();
        _model.PropName = propName;
        _model.Id = propName.SanitizeForHtmlId();
        _model.Kind = GetInputKind(typeof(TProp));
        _model.Label = propName.Humanize();
    }

    public InputModelBuilder<T, TProp> WithKind(InputKind kind)
    {
        _model.Kind = kind;
        return this;
    }

    public InputModelBuilder<T, TProp> WithName(string name)
    {
        _model.PropName = name;
        return this;
    }

    public InputModelBuilder<T, TProp> WithLabel(string label)
    {
        _model.Label = label;
        return this;
    }

    public InputModelBuilder<T, TProp> WithPlaceholder(string placeholder)
    {
        _model.Placeholder = placeholder;
        return this;
    }

    public InputModelBuilder<T, TProp> WithCssClass(string cssClass)
    {
        _model.CssClass = cssClass;
        return this;
    }

    public InputModelBuilder<T, TProp> WithValue(string value)
    {
        _model.Value = value;
        return this;
    }

    public InputModelBuilder<T, TProp> WithAttribute(string key, string value)
    {
        _model.Attributes[key] = value;
        return this;
    }

    public InputModelBuilder<T, TProp> WithOptions(IEnumerable<KeyValuePair<string, string>> options)
    {
        _model.Options = options.ToList();
        return this;
    }

    async Task<IInputModel> IInputModelBuilder.Build()
    {
        return await base.Build();
    }

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
