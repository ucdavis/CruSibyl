namespace Htmx.Components.Models;

public class HtmxViewInfo
{
    public string ViewName { get; set; } = "";
    public object Model { get; set; } = null!;
    public OobTargetRelation TargetRelation { get; set; } = OobTargetRelation.None;
    public string? TargetSelector { get; set; } = null;
}

public enum OobTargetRelation
{
    OuterHtml,
    InnerHtml,
    AfterBegin,
    BeforeEnd,
    BeforeBegin,
    AfterEnd,
    None,
}