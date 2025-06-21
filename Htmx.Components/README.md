# Htmx.Components

A comprehensive ASP.NET Core library that provides HTMX-enabled UI components for building interactive web applications with server-side rendering.

## Features

### 🚀 Core Components
- **Table Component**: Interactive data tables with pagination, sorting, filtering, and CRUD operations
- **Navigation Bar**: Attribute-based or builder-pattern navigation with authorization support
- **Authentication Status**: Configurable authentication UI with profile display
- **Form Controls**: HTMX-enabled input components with validation support

### 🔧 HTMX Integration
- **Multi-Swap View Results**: Return multiple HTMX views in a single response
- **Out-of-Band Updates**: Automatic injection of component updates
- **Page State Management**: Encrypted client-side state with server-side validation
- **Authentication Retry**: Seamless re-authentication for expired sessions

### 🛡️ Authorization & Security
- **Resource-based Authorization**: Fine-grained permissions with custom requirements
- **Role-based Access Control**: Configurable role services
- **Secure Page State**: Encrypted state management with data protection

### 📱 Responsive Design
- **DaisyUI Integration**: Beautiful, responsive components out of the box
- **Tailwind CSS Support**: Utility-first styling approach
- **Mobile-friendly**: Touch-optimized interactions

## Quick Start

### 1. Installation

```bash
dotnet add package Htmx.Components
```

### 2. Configuration

```csharp
// Program.cs
builder.Services.AddHtmxComponents(options =>
{
    options.WithModelHandlerRegistry((registry, serviceProvider) =>
    {
        // Register your model handlers
        ModelHandlerAttributeRegistrar.RegisterAll(registry);
    });
    
    options.WithPermissionRequirementFactory<YourPermissionFactory>();
    options.WithResourceOperationRegistry<YourResourceRegistry>();
    options.WithUserIdClaimType("your-claim-type");
});

builder.Services.AddControllersWithViews()
    .AddHtmxComponentsApplicationPart();

// Configure middleware
app.UseHtmxPageState();
app.UseAuthentication();
app.UseAuthorization();
```

### 3. Basic Usage

#### Table Component

```csharp
// Controller
[Route("Users")]
public class UsersController : Controller
{
    [NavAction(DisplayName = "Users", Icon = "fas fa-users")]
    public async Task<IActionResult> Index()
    {
        var tableModel = await _modelHandler.BuildTableModel();
        return View(tableModel);
    }
}
```

```cshtml
<!-- View -->
@model ITableModel
@await Component.InvokeAsync("Table", Model)
```

#### Navigation with Attributes

```csharp
[NavActionGroup(DisplayName = "Administration", Order = 10)]
public class AdminController : Controller
{
    [NavAction(DisplayName = "Users", Icon = "fas fa-users", Order = 1)]
    public IActionResult Users() => View();
    
    [NavAction(DisplayName = "Settings", Icon = "fas fa-cog", Order = 2)]
    public IActionResult Settings() => View();
}
```

#### Authentication Status

```cshtml
<!-- Layout -->
<div class="navbar-end">
    @await Component.InvokeAsync("NavBar")
    @await Component.InvokeAsync("AuthStatus")
</div>
```

## Advanced Features

### Multi-Swap Responses

```csharp
public IActionResult UpdateData()
{
    return new MultiSwapViewResult()
        .WithMainContent("_UpdatedContent", model)
        .WithOobContent("NavBar", navigationModel)
        .WithOobContent("AuthStatus", authModel);
}
```

### Custom Authorization

```csharp
public class CustomPermissionFactory : IPermissionRequirementFactory
{
    public IAuthorizationRequirement ForOperation(string operation, Type resourceType)
    {
        return new CustomPermissionRequirement(operation, resourceType);
    }
}
```

### Page State Management

```csharp
public class MyController : Controller
{
    private readonly IPageState _pageState;
    
    public IActionResult SaveFilter(string filterValue)
    {
        _pageState.Set("filters", "current", filterValue);
        return Ok();
    }
    
    public IActionResult LoadFilter()
    {
        var filter = _pageState.Get<string>("filters", "current");
        return Ok(filter);
    }
}
```

### Filter Attributes

```csharp
[TableEditAction] // Automatically injects table updates
[AuthStatusUpdate] // Updates auth status on completion
public async Task<IActionResult> CreateUser(CreateUserModel model)
{
    // Your logic here
    return Ok(result);
}
```

## Architecture

### Model Handlers
Model handlers provide the bridge between your domain models and UI components:

```csharp
[ModelHandler<User>]
public class UserModelHandler : ModelHandler<User, int>
{
    protected override void ConfigureTable(TableBuilder<User> builder)
    {
        builder.Column(u => u.Name).Sortable().Filterable();
        builder.Column(u => u.Email).Filterable();
        builder.Column(u => u.CreatedAt).Sortable();
    }
}
```

### View Customization
Override default view paths to customize component rendering:

```csharp
options.WithViewOverrides(views =>
{
    views.Table.Row = "_CustomTableRow";
    views.NavBar = "CustomNavBar";
    views.AuthStatus = "CustomAuthStatus";
});
```

### Builder Pattern Navigation

```csharp
options.WithNavBuilder(nav =>
{
    nav.WithAction(a => a
        .WithLabel("Dashboard")
        .WithIcon("fas fa-dashboard")
        .WithHxGet("/dashboard")
        .WithHxTarget("#main-content"));
        
    nav.WithGroup(g => g
        .WithLabel("Reports")
        .WithIcon("fas fa-chart-bar")
        .WithAction(a => a.WithLabel("Sales").WithHxGet("/reports/sales"))
        .WithAction(a => a.WithLabel("Users").WithHxGet("/reports/users")));
});
```

## Dependencies

- ASP.NET Core 6.0+
- HTMX 1.8+
- System.Text.Json
- Microsoft.AspNetCore.DataProtection

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

- 📖 [Documentation](link-to-docs)
- 🐛 [Issues](link-to-issues)
- 💬 [Discussions](link-to-discussions)

## Acknowledgments

- Built with [HTMX](https://htmx.org/)
- Styled with [DaisyUI](https://daisyui.com/) and [Tailwind CSS](https://tailwindcss.com/)
- Inspired by modern component-based architectures