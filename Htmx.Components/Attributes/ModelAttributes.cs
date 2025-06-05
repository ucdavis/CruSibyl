namespace Htmx.Components.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class ModelCreateAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class ModelReadAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class ModelUpdateAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class ModelDeleteAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class ModelConfigAttribute : Attribute { }