namespace Htmx.Components.Input;

public interface IInputProvider
{
    InputField BuildInputField(Action<InputFieldBuilder> config);
    InputSet BuildInputSet(Action<InputSetBuilder> config);
    InputSet BuildInputSet<T>() where T : class;
    InputSet BuildInputSet<T>(T fromModel) where T : class;
    T BuildModel<T>(InputSet inputSet) where T : class, new();
    object? GetValue(InputField inputField);
    T? GetValue<T>(InputField inputField);
}

public class InputProvider : IInputProvider
{
    public InputField BuildInputField(Action<InputFieldBuilder> config)
    {
        var builder = new InputFieldBuilder();
        config(builder);
        return builder.Build();
    }

    public InputSet BuildInputSet(Action<InputSetBuilder> config)
    {
        var builder = new InputSetBuilder();
        config(builder);
        return builder.Build();
    }

    public InputSet BuildInputSet<T>() where T : class
    {
        return BuildInputSetFromModel(Activator.CreateInstance<T>()!);
    }

    public InputSet BuildInputSet<T>(T fromModel) where T : class
    {
        return BuildInputSetFromModel(fromModel);
    }

    private InputSet BuildInputSetFromModel<T>(T model) where T : class
    {
        var builder = new InputSetBuilder();
        foreach (var prop in typeof(T).GetProperties())
        {
            builder.AddField(f => f
                .WithName(prop.Name)
                .WithLabel(prop.Name)
                .WithType(MapType(prop.PropertyType))
                .WithValue(prop.GetValue(model)?.ToString() ?? "")
            );
        }
        return builder.Build();
    }

    private string MapType(Type type)
    {
        if (type == typeof(string)) return "text";
        if (type == typeof(int) || type == typeof(long)) return "number";
        if (type == typeof(DateTime)) return "date";
        if (type == typeof(bool)) return "checkbox";
        return "text";
    }

    public T BuildModel<T>(InputSet inputSet) where T : class, new()
    {
        var model = new T();
        var type = typeof(T);
        foreach (var field in inputSet.Fields)
        {
            var prop = type.GetProperty(field.Name);
            if (prop == null) continue;
            var value = Convert.ChangeType(field.Value, prop.PropertyType);
            prop.SetValue(model, value);
        }
        return model;
    }

    public object? GetValue(InputField inputField)
    {
        return inputField.Value;
    }

    public T? GetValue<T>(InputField inputField)
    {
        if (inputField.Value == null) return default;
        return (T)Convert.ChangeType(inputField.Value, typeof(T));
    }
}
