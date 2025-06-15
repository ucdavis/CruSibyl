namespace Htmx.Components.Attributes;


[AttributeUsage(AttributeTargets.Method)]
public class ModelConfigAttribute : Attribute
{
    public string ModelTypeId { get; }
    public ModelConfigAttribute(string modelTypeId) => ModelTypeId = modelTypeId;
}