using System;

namespace Htmx.Components.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class AuthStatusUpdateAttribute : Attribute
{
}