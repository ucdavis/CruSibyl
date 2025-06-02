namespace Htmx.Components.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class NavActionGroupAttribute : Attribute
{
    public int Order { get; set; } = 0;
    public string? Icon { get; set; }
    public string? DisplayName { get; set; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class NavActionAttribute : Attribute
{
    public int Order { get; set; } = 0;
    public string? Icon { get; set; }
    public string? DisplayName { get; set; }
    public string? HttpMethod { get; set; } = "GET";
    public bool PushUrl { get; set; }
}