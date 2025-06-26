namespace Htmx.Components.Models;

/// <summary>
/// A model that can provide the target disposition and selector for an out-of-band (OOB) request.
/// </summary>
public interface IOobTargetable
{
    string? TargetSelector { get; }
    OobTargetDisposition? TargetDisposition { get; }
}

public enum OobTargetDisposition
{
    OuterHtml,
    InnerHtml,
    AfterBegin,
    BeforeEnd,
    BeforeBegin,
    AfterEnd,
    None,
    Delete,
}