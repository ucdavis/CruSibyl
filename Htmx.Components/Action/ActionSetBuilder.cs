namespace Htmx.Components.Action;

public class ActionSetBuilder
{
    private readonly ActionSet _set = new();

    public ActionSetBuilder AddModel(Action<ActionModelBuilder> configure)
    {
        var builder = new ActionModelBuilder();
        configure(builder);
        _set.Items.Add(builder.Build());
        return this;
    }

    public ActionSetBuilder AddGroup(Action<ActionGroupBuilder> configure)
    {
        var builder = new ActionGroupBuilder();
        configure(builder);
        _set.Items.Add(builder.Build());
        return this;
    }

    public ActionSetBuilder AddItem(IActionItem item)
    {
        _set.Items.Add(item);
        return this;
    }

    public ActionSetBuilder AddRange(IEnumerable<IActionItem> items)
    {
        _set.Items.AddRange(items);
        return this;
    }

    public ActionSet Build() => _set;
}
