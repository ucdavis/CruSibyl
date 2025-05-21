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
    protected internal readonly TModel _model = new();
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