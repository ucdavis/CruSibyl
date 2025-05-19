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
    private readonly List<Task> _buildTasks = new();


    protected BuilderBase(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }


    public ActionContext ActionContext
    {
        get
        {
            var actionContextAccessor = _serviceProvider.GetRequiredService<IActionContextAccessor>();
            return actionContextAccessor.GetValidActionContext();
        }
    }

    protected void AddBuildTask(Task task)
    {
        _buildTasks.Add(task);
    }

    protected void AddBuildTask(Func<Task> taskFunc)
    {
        var task = taskFunc();
        _buildTasks.Add(task);
    }

    protected void AddBuildTask(Action action)
    {
        var task = Task.Run(action);
        _buildTasks.Add(task);
    }

    internal virtual async Task<TModel> Build()
    {
        await Task.WhenAll(_buildTasks);
        return _model;
    }
}