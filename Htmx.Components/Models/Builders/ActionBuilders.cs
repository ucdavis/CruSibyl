using Htmx.Components.Models;

namespace Htmx.Components.Models.Builders;

public abstract class ActionItemsBuilder<TBuilder, TSet> : BuilderBase<TBuilder, TSet>
    where TBuilder : ActionItemsBuilder<TBuilder, TSet>
    where TSet : class, IActionSet, new()
{
    protected ActionItemsBuilder(IServiceProvider serviceProvider)
        : base(serviceProvider) { }

    public TBuilder AddModel(Action<ActionModelBuilder> configure)
    {
        AddBuildTask(BuildPhase.Actions, async () =>
        {
            var actionModelBuilder = new ActionModelBuilder(_serviceProvider);
            configure(actionModelBuilder);
            var actionModel = await actionModelBuilder.Build();
            _model.Items.Add(actionModel);
        });
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
        AddBuildTask(BuildPhase.Actions, async () =>
        {
            var actionGroupBuilder = new ActionGroupBuilder(_serviceProvider);
            configure(actionGroupBuilder);
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

public class ActionModelBuilder : BuilderBase<ActionModelBuilder, ActionModel>
{
    public ActionModelBuilder(IServiceProvider serviceProvider)
        : base(serviceProvider) { }

    public ActionModelBuilder WithLabel(string label)
    {
        _model.Label = label;
        return this;
    }

    public ActionModelBuilder WithIcon(string icon)
    {
        _model.Icon = icon;
        return this;
    }

    public ActionModelBuilder WithClass(string cssClass)
    {
        _model.CssClass = cssClass;
        return this;
    }

    public ActionModelBuilder WithAttribute(string name, string value)
    {
        _model.Attributes[name] = value;
        return this;
    }

    public ActionModelBuilder WithHxGet(string url) => WithAttribute("hx-get", url);
    public ActionModelBuilder WithHxPost(string url) => WithAttribute("hx-post", url);
    public ActionModelBuilder WithHxTarget(string target) => WithAttribute("hx-target", target);
    public ActionModelBuilder WithHxSwap(string swap) => WithAttribute("hx-swap", swap);
    public ActionModelBuilder WithHxPushUrl(string pushUrl = "true") => WithAttribute("hx-push-url", pushUrl);
    public ActionModelBuilder WithHxInclude(string selector) => WithAttribute("hx-include", selector);
}