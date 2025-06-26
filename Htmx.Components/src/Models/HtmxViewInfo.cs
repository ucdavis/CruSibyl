namespace Htmx.Components.Models;

public class HtmxViewInfo
{
    public string ViewName { get; set; } = "";
    public object Model { get; set; } = null!;
    public OobTargetDisposition TargetDisposition { get; set; } = OobTargetDisposition.None;
    public string? TargetSelector { get; set; } = null;
}

