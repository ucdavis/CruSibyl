using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;

namespace Htmx.Components.Models.Builders;

public class InputSetBuilder<T> : BuilderBase<InputSetBuilder<T>, InputSet>
    where T : class
{

    public InputSetBuilder(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
    }

    public InputSetBuilder<T> AddInput<TProp>(Expression<Func<T, TProp>> propSelector,
        Action<InputModelBuilder<T, TProp>> configure)
    {
        var builder = new InputModelBuilder<T, TProp>(_serviceProvider, propSelector);
        configure(builder);
        AddBuildTask(async () =>
        {
            var inputModel = await builder.Build();
            _model.Inputs.Add(inputModel);
        });
        return this;
    }

    public InputSetBuilder<T> AddInput(IInputModel inputModel)
    {
        _model.Inputs.Add(inputModel);
        return this;
    }

    public InputSetBuilder<T> AddRange(IEnumerable<IInputModel> inputModels)
    {
        _model.Inputs.AddRange(inputModels);
        return this;
    }

    public InputSetBuilder<T> WithLabel(string label)
    {
        _model.Label = label;
        return this;
    }
}
