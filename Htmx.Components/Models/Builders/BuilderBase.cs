using Htmx.Components.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Htmx.Components.Models.Builders;

public abstract class BuilderBase<TBuilder, TModel>
    where TBuilder : BuilderBase<TBuilder, TModel>
    where TModel : class, new()
{
    protected readonly TModel _model = new();
    private bool _isBuilt = false;

    protected readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<BuildPhase, List<Func<Task>>> _buildPhaseTasks;

    protected BuilderBase(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _buildPhaseTasks = [];
        foreach (BuildPhase phase in Enum.GetValues(typeof(BuildPhase)))
        {
            _buildPhaseTasks[phase] = [];
        }
    }

    /// This is used to access the model before it is built. Anything accessing the model via this property
    /// should be careful about relying on the state of any given model property.
    internal TModel IncompleteModel => _model;

    /// This is used to access the model after it is built. It will throw an exception if the model has not been built yet.
    internal TModel Model
    {
        get
        {
            if (!_isBuilt)
            {
                throw new InvalidOperationException("Builder has not been built yet. Call Build() before accessing the model.");
            }
            return _model;
        }
    }

    public ActionContext ActionContext
    {
        get
        {
            var actionContextAccessor = _serviceProvider.GetRequiredService<IActionContextAccessor>();
            return actionContextAccessor.GetValidActionContext();
        }
    }

    protected void AddBuildTask(BuildPhase phase, Task task)
    {
        _buildPhaseTasks[phase].Add(() => task);
    }

    protected void AddBuildTask(BuildPhase phase, Func<Task> taskFunc)
    {
        _buildPhaseTasks[phase].Add(taskFunc);
    }

    protected void AddBuildTask(BuildPhase phase, Action action)
    {
        _buildPhaseTasks[phase].Add(() => Task.Run(action));
    }

    internal virtual async Task<TModel> Build()
    {
        // Execute all build tasks in the order of phases
        foreach (var phase in Enum.GetValues<BuildPhase>())
        {
            var tasks = _buildPhaseTasks[(BuildPhase)phase].Select(f => f());
            await Task.WhenAll(tasks);
        }

        _isBuilt = true;
        return _model;
    }
}

// Helps to ensure that the build tasks are executed in the correct order
// ie: InputModels should be built before ColumnModels
public enum BuildPhase
{
    Inputs = 0,
    Columns = 1,
    Actions = 2,
    Other = 3
}