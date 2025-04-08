namespace Htmx.Components.Action;

public class ActionGroupBuilder
{
    private readonly ActionGroup _group = new();

    public ActionGroupBuilder WithLabel(string label)
    {
        _group.Label = label;
        return this;
    }

    public ActionGroupBuilder WithIcon(string icon)
    {
        _group.Icon = icon;
        return this;
    }

    public ActionGroupBuilder WithClass(string cssClass)
    {
        _group.CssClass = cssClass;
        return this;
    }

    public ActionGroupBuilder AddModel(Action<ActionModelBuilder> configure)
    {
        var builder = new ActionModelBuilder();
        configure(builder);
        _group.Items.Add(builder.Build());
        return this;
    }

    public ActionGroupBuilder AddItem(IActionItem item)
    {
        _group.Items.Add(item);
        return this;
    }

    public ActionGroupBuilder AddRange(IEnumerable<IActionItem> items)
    {
        _group.Items.AddRange(items);
        return this;
    }

    public ActionGroup Build() => _group;
}
