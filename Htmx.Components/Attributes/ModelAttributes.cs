namespace Htmx.Components.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class ModelCreateAttribute : Attribute
{
    public string ModelTypeId { get; }
    public ModelCreateAttribute(string modelTypeId) => ModelTypeId = modelTypeId;
}

[AttributeUsage(AttributeTargets.Method)]
public class ModelReadAttribute : Attribute
{
    public string ModelTypeId { get; }
    public ModelReadAttribute(string modelTypeId) => ModelTypeId = modelTypeId;
}

[AttributeUsage(AttributeTargets.Method)]
public class ModelUpdateAttribute : Attribute
{
    public string ModelTypeId { get; }
    public ModelUpdateAttribute(string modelTypeId) => ModelTypeId = modelTypeId;
}

[AttributeUsage(AttributeTargets.Method)]
public class ModelDeleteAttribute : Attribute
{
    public string ModelTypeId { get; }
    public ModelDeleteAttribute(string modelTypeId) => ModelTypeId = modelTypeId;
}

[AttributeUsage(AttributeTargets.Method)]
public class ModelConfigAttribute : Attribute
{
    public string ModelTypeId { get; }
    public ModelConfigAttribute(string modelTypeId) => ModelTypeId = modelTypeId;
}