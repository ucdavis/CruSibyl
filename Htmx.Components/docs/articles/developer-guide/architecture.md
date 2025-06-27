# Architecture Overview

Htmx.Components is built around a modular architecture that leverages ASP.NET Core's dependency injection and MVC patterns to provide a seamless HTMX integration experience.

## Core Components

### 1. Model Handlers ([`ModelHandler<T, TKey>`](../../api/Htmx.Components.Models.ModelHandler-2.html))

Model handlers are the central abstraction that define how data models are processed, displayed, and manipulated:

```csharp
public class ModelHandler<T, TKey> : ModelHandler
    where T : class
{
    public Expression<Func<T, TKey>> KeySelector { get; set; }
    public Func<IQueryable<T>>? GetQueryable { get; internal set; }
    public Func<T, Task<Result<T>>>? CreateModel { get; internal set; }
    public Func<T, Task<Result<T>>>? UpdateModel { get; internal set; }
    public Func<TKey, Task<Result>>? DeleteModel { get; internal set; }
}
```

**Key Features:**
- CRUD operation definitions
- Table model building and data fetching
- Input model configuration
- Integration with authorization system

### 2. Table System

The table system provides a comprehensive data grid solution:

#### TableModel ([`TableModel<T, TKey>`](../../api/Htmx.Components.Table.Models.TableModel-2.html))
- Represents the complete table structure
- Contains columns, rows, pagination state
- Supports sorting, filtering, and CRUD operations

#### TableColumnModel ([`TableColumnModel<T, TKey>`](../../api/Htmx.Components.Table.Models.TableColumnModel-2.html))
- Defines individual column behavior
- Supports custom rendering, filtering, and actions
- Type-safe property binding via expressions

#### TableProvider ([`ITableProvider`](../../api/Htmx.Components.Table.ITableProvider.html))
- Handles data fetching and query building
- Applies filtering, sorting, and pagination
- Works with Entity Framework Core queryables

### 3. Navigation System

#### INavProvider
The navigation system supports two approaches:

**Attribute-Based Navigation ([`AttributeNavProvider`](../../api/Htmx.Components.NavBar.AttributeNavProvider.html)):**
```csharp
[NavActionGroup(Order = 1, DisplayName = "Admin", Icon = "fas fa-cog")]
public class AdminController : Controller
{
    [NavAction(Order = 1, DisplayName = "Users", Icon = "fas fa-users")]
    public IActionResult Users() => View();
}
```

**Builder-Based Navigation ([`BuilderBasedNavProvider`](../../api/Htmx.Components.NavBar.BuilderBasedNavProvider.html)):**
```csharp
services.AddHtmxComponents(options =>
{
    options.WithNavBuilder(nav =>
    {
        nav.AddAction(a => a
            .WithLabel("Dashboard")
            .WithIcon("fas fa-dashboard")
            .WithHxGet("/Dashboard"));
    });
});
```

### 4. State Management

#### PageState ([`IPageState`](../../api/Htmx.Components.State.IPageState.html))
Provides encrypted, client-side state management:

```csharp
public interface IPageState
{
    T? Get<T>(string partition, string key);
    void Set<T>(string partition, string key, T value);
    T GetOrCreate<T>(string partition, string key, Func<T> factory);
    string Encrypted { get; }
    bool IsDirty { get; }
}
```

**Features:**
- Partitioned state organization
- Automatic encryption/decryption
- Change tracking
- HTTP header-based transport

### 5. Filter System

The framework includes sophisticated result filters for HTMX integration:

#### OobResultFilterBase
Base class for out-of-band (OOB) content injection:

```csharp
public abstract class OobResultFilterBase<T> : IAsyncResultFilter
    where T : Attribute
{
    protected abstract Task UpdateMultiSwapViewResultAsync(
        T attribute, 
        MultiSwapViewResult multiSwapViewResult, 
        ResultExecutingContext context);
}
```

#### Specialized Filters
- `TableOobRefreshFilter`: Updates table components
- `TableOobEditFilter`: Handles inline editing
- [`NavActionResultFilter`](../../api/Htmx.Components.NavBar.Internal.NavActionResultFilter.html): Updates navigation state
- [`AuthStatusUpdateFilter`](../../api/Htmx.Components.AuthStatus.Internal.AuthStatusUpdateFilter.html): Refreshes authentication status
- [`PageStateOobInjectorFilter`](../../api/Htmx.Components.Filters.PageStateOobInjectorFilter.html): Manages state synchronization

### 6. Authorization Integration

#### IPermissionRequirementFactory
Creates authorization requirements:

```csharp
public interface IPermissionRequirementFactory
{
    IAuthorizationRequirement ForOperation(string resource, string operation);
    IAuthorizationRequirement ForRoles(params string[] roles);
}
```

#### IResourceOperationRegistry
Registers resource-operation pairs with the authorization system:

```csharp
public interface IResourceOperationRegistry
{
    Task Register(string resource, string operation);
}
```

#### AuthorizationMetadataService
Provides caching and evaluation of authorization metadata:

```csharp
public class AuthorizationMetadataService : IAuthorizationMetadataService
{
    public async Task<bool> IsAuthorizedAsync(
        ControllerActionDescriptor descriptor, 
        ClaimsPrincipal user);
}
```

## Data Flow Architecture

### 1. Request Processing

```
HTTP Request → Middleware → Controller → ModelHandler → View Components → Response
```

1. **PageStateMiddleware** extracts encrypted state from headers
2. **Controller actions** process requests using ModelHandlers
3. **Result filters** transform responses for HTMX
4. **View components** render UI fragments
5. **MultiSwapViewResult** coordinates multiple content updates

### 2. Table Data Flow

```
Query → TableProvider → EF Core → Filtering/Sorting → Pagination → TableModel → Views
```

1. **ModelHandler** provides base queryable
2. **TableProvider** applies state-based transformations
3. **EF Core** executes optimized queries
4. **TableModel** structures data for rendering
5. **Partial views** render table fragments

### 3. State Synchronization

```
Client State → HTTP Header → PageStateMiddleware → IPageState → Result Filters → OOB Updates
```

1. Client sends encrypted state in headers
2. Middleware deserializes state into [`IPageState`](../../api/Htmx.Components.State.IPageState.html)
3. Controllers modify state during processing
4. Result filters detect state changes
5. OOB updates sync client-side state

## Extension Points

### 1. Custom Model Handlers

Create specialized model handlers for complex scenarios:

```csharp
public class AuditableModelHandler<T, TKey> : ModelHandler<T, TKey>
    where T : class, IAuditable
{
    // Add audit-specific functionality
}
```

### 2. Custom Table Columns

Implement specialized column types:

```csharp
public class ImageColumnBuilder<T, TKey> : TableColumnModelBuilder<T, TKey>
{
    public ImageColumnBuilder<T, TKey> WithImageRenderer(string imageUrlProperty)
    {
        // Custom image rendering logic
        return this;
    }
}
```

### 3. Custom View Components

Create reusable UI components:

```csharp
public class CustomWidgetViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(CustomWidgetModel model)
    {
        return View(model);
    }
}
```

### 4. Custom Authorization

Implement domain-specific authorization:

```csharp
public class ProjectBasedAuthRequirementFactory : IPermissionRequirementFactory
{
    public IAuthorizationRequirement ForOperation(string resource, string operation)
    {
        return new ProjectOperationRequirement(resource, operation);
    }
}
```

## Performance Considerations

### 1. Caching Strategy

- **Authorization metadata**: Cached for 10 minutes
- **Authorization results**: Cached for 2 minutes per user/policy
- **Compiled expressions**: Cached indefinitely via FastExpressionCompiler

### 2. Query Optimization

- Uses `IQueryable<T>` for deferred execution
- Applies filtering before pagination
- Leverages EF Core query compilation

### 3. State Management

- Encrypted state prevents tampering
- Partitioned organization reduces serialization overhead
- Change tracking minimizes unnecessary updates

## Testing Strategies

### 1. Unit Testing

Focus on isolated component testing:

```csharp
[Test]
public async Task ModelHandler_BuildTableModel_ReturnsConfiguredColumns()
{
    // Arrange
    var handler = new ModelHandler<User, int>(options, tableProvider, pageState);
    
    // Act
    var tableModel = await handler.BuildTableModelAsync();
    
    // Assert
    Assert.That(tableModel.Columns, Has.Count.EqualTo(3));
}
```

### 2. Integration Testing

Test complete request flows:

```csharp
[Test]
public async Task TableRefresh_WithFiltering_ReturnsFilteredResults()
{
    // Test full HTMX request/response cycle
}
```

### 3. Authorization Testing

Verify security constraints:

```csharp
[Test]
public async Task NavProvider_ExcludesUnauthorizedActions()
{
    // Test that unauthorized actions are filtered
}
```

This architecture provides a solid foundation for building complex HTMX applications while maintaining separation of concerns and extensibility.