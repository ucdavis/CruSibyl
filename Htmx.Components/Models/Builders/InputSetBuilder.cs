using System.Linq.Expressions;

namespace Htmx.Components.Models.Builders;

public class InputSetBuilder<T>
    where T : class
{
    private readonly InputSet _set = new();

    public InputSetBuilder<T> AddInput<TProp>(Expression<Func<T, TProp>> propSelector, 
        Action<InputModelBuilder<T, TProp>> configure)
    {
        var builder = new InputModelBuilder<T, TProp>(propSelector);
        configure(builder);
        _set.Inputs.Add(builder.Build());
        return this;
    }

    public InputSetBuilder<T> AddInput(IInputModel inputModel)
    {
        _set.Inputs.Add(inputModel);
        return this;
    }

    public InputSetBuilder<T> AddRange(IEnumerable<IInputModel> inputModels)
    {
        _set.Inputs.AddRange(inputModels);
        return this;
    }

    public InputSetBuilder<T> WithLabel(string label)
    {
        _set.Label = label;
        return this;
    }

    public InputSet Build() => _set;
}
