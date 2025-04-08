namespace Htmx.Components.Action;

public class ActionModelBuilder
{
    private readonly ActionModel _action = new();

    public ActionModelBuilder WithLabel(string label)
    {
        _action.Label = label;
        return this;
    }

    public ActionModelBuilder WithIcon(string icon)
    {
        _action.Icon = icon;
        return this;
    }

    public ActionModelBuilder WithClass(string cssClass)
    {
        _action.CssClass = cssClass;
        return this;
    }

    public ActionModelBuilder WithAttribute(string name, string value)
    {
        _action.Attributes[name] = value;
        return this;
    }

    public ActionModelBuilder WithHxGet(string url) => WithAttribute("hx-get", url);
    public ActionModelBuilder WithHxPost(string url) => WithAttribute("hx-post", url);
    public ActionModelBuilder WithHxTarget(string target) => WithAttribute("hx-target", target);
    public ActionModelBuilder WithHxSwap(string swap) => WithAttribute("hx-swap", swap);
    public ActionModelBuilder WithHxPushUrl(string pushUrl = "true") => WithAttribute("hx-push-url", pushUrl);
    public ActionModelBuilder WithHxInclude(string selector) => WithAttribute("hx-include", selector);

    public ActionModel Build() => _action;
}



