# API Reference

Welcome to the HTMX Components API documentation. This section provides detailed information about all the classes, interfaces, and methods available in the library.

## Core Namespaces

### [Htmx.Components](Htmx.Components.yml)
The main namespace containing the core functionality including:
- Configuration and setup extensions
- Component options and settings
- Page state management

### [Htmx.Components.Models](Htmx.Components.Models.yml)
Data models and configuration classes for components:
- Action models and builders
- Input models and validation
- Table models and configuration
- Result handling and error management

### [Htmx.Components.Table](Htmx.Components.Table.yml)
Table component implementation:
- Table providers and view components
- Column definitions and behaviors
- Data binding and display options

### [Htmx.Components.NavBar](Htmx.Components.NavBar.yml)
Navigation bar components:
- Navigation providers
- Attribute-based navigation
- Builder-based navigation configuration

### [Htmx.Components.AuthStatus](Htmx.Components.AuthStatus.yml)
Authentication status components:
- Authentication status providers
- View components for auth display
- Update filters and event handling

### [Htmx.Components.Authorization](Htmx.Components.Authorization.yml)
Authorization and permission system:
- Permission requirement factories
- Resource operation registries
- CRUD operation constants

### [Htmx.Components.State](Htmx.Components.State.yml)
Page state management:
- Page state interfaces and implementations
- State constants and keys
- Form and table state handling

### [Htmx.Components.Services](Htmx.Components.Services.yml)
Core services and infrastructure:
- Model handlers and factories
- Authorization metadata services
- Role services and registries

### [Htmx.Components.Attributes](Htmx.Components.Attributes.yml)
Attributes for declarative configuration:
- Navigation action attributes
- Table action attributes
- Model configuration attributes

### [Htmx.Components.Extensions](Htmx.Components.Extensions.yml)
Extension methods and helpers:
- Action context extensions
- Expression helpers
- String utilities

### [Htmx.Components.TagHelpers](Htmx.Components.TagHelpers.yml)
ASP.NET Core Tag Helpers:
- Page state tag helpers
- Component rendering helpers

### [Htmx.Components.Input.Validation](Htmx.Components.Input.Validation.yml)
Input validation system:
- Validation interfaces and results
- Validator registry
- Custom validation support

## Getting Started with the API

The API is designed to be intuitive and follows common .NET patterns. Here are some key entry points:

1. **Configuration**: Start with `ServiceCollectionExtensions.AddHtmxComponents()`
2. **Models**: Use the builder classes in `Htmx.Components.Models.Builders`
3. **Components**: Leverage view components like `TableViewComponent` and `NavBarViewComponent`
4. **State**: Manage page state through `IPageState` implementations

## Common Usage Patterns

Most interactions with the API will involve:
- Configuring services during application startup
- Creating model configurations using builders
- Implementing custom providers for advanced scenarios
- Using attributes for declarative configuration

For detailed examples and usage patterns, see the [User Guide](../articles/user-guide/basic-usage.md) and [Developer Guide](../articles/developer-guide/architecture.md).
