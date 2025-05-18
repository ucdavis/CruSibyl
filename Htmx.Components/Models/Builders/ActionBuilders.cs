using Htmx.Components.Models;

namespace Htmx.Components.Models.Builders;

public abstract class ActionItemsBuilder<TBuilder, TSet> : BuilderBase<TBuilder, TSet>
    where TBuilder : ActionItemsBuilder<TBuilder, TSet>
    where TSet : class, IActionSet, new()
{
    protected ActionItemsBuilder(IServiceProvider serviceProvider)
        : base(serviceProvider) { }

    public TBuilder AddModel(Action<ActionModel> configure)
    {
        var model = new ActionModel();
        configure(model);
        _model.Items.Add(model);
        return (TBuilder)this;
    }

    public TBuilder AddItem(IActionItem item)
    {
        _model.Items.Add(item);
        return (TBuilder)this;
    }

    public TBuilder AddRange(IEnumerable<IActionItem> items)
    {
        _model.Items.AddRange(items);
        return (TBuilder)this;
    }
}

public class ActionSetBuilder : ActionItemsBuilder<ActionSetBuilder, ActionSet>
{
    public ActionSetBuilder(IServiceProvider serviceProvider)
        : base(serviceProvider) { }

    public ActionSetBuilder AddGroup(Action<ActionGroupBuilder> configure)
    {
        var actionGroupBuilder = new ActionGroupBuilder(_serviceProvider);
        configure(actionGroupBuilder);
        AddBuildTask(async () =>
        {
            var actionGroup = await actionGroupBuilder.Build();
            _model.Items.Add(actionGroup);
        });
        return this;
    }
}

public class ActionGroupBuilder : ActionItemsBuilder<ActionGroupBuilder, ActionGroup>
{
    public ActionGroupBuilder(IServiceProvider serviceProvider)
        : base(serviceProvider) { }

    public ActionGroupBuilder WithLabel(string label)
    {
        _model.Label = label;
        return this;
    }

    public ActionGroupBuilder WithIcon(string icon)
    {
        _model.Icon = icon;
        return this;
    }

    public ActionGroupBuilder WithClass(string cssClass)
    {
        _model.CssClass = cssClass;
        return this;
    }
}