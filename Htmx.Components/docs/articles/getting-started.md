# Getting Started

This guide will help you set up HTMX Components in your ASP.NET Core application.

## Prerequisites

- .NET 8.0 or later
- ASP.NET Core application
- Basic familiarity with HTMX concepts

## Installation

Add the HTMX Components package to your project:

```bash
dotnet add package Htmx.Components
```

## Basic Setup

### 1. Configure Services

In your `Program.cs`, add the HTMX Components services:

```csharp
using Htmx.Components;

var builder = WebApplication.CreateBuilder(args);

// Add HTMX Components before MVC
builder.Services.AddHtmxComponents(options =>
{
    // Required: Configure authorization
    options.WithAuthorizationRequirementFactory<SimplePermissionFactory>();
    options.WithResourceOperationRegistry<InMemoryResourceRegistry>();
    
    // Optional: Configure navigation
    options.WithNavBuilder(nav =>
    {
        nav.AddAction(action => action
            .WithLabel("Home")
            .WithIcon("fas fa-home")
            .WithHxGet("/"));
    });
});

// Add MVC and include HTMX Components controllers
builder.Services.AddMvc()
    .AddHtmxComponentsApplicationPart();

var app = builder.Build();

// Configure pipeline - IMPORTANT: Add middleware before auth
app.UseHtmxPageState();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### 2. Create Required Implementations

You need to implement the authorization interfaces:

```csharp
// Simple permission factory for getting started
public class SimplePermissionFactory : IAuthorizationRequirementFactory
{
    public IAuthorizationRequirement ForOperation(string resource, string operation)
    {
        return new OperationAuthorizationRequirement { Name = $"{resource}:{operation}" };
    }

    public IAuthorizationRequirement ForRoles(params string[] roles)
    {
        return new RolesAuthorizationRequirement(roles);
    }
}

// Simple resource registry for getting started
public class InMemoryResourceRegistry : IResourceOperationRegistry
{
    private readonly HashSet<string> _registeredOperations = new();

    public Task Register(string resource, string operation)
    {
        _registeredOperations.Add($"{resource}:{operation}");
        return Task.CompletedTask;
    }
}
```

### 3. Add HTMX to Your Layout

Include HTMX in your layout file (`_Layout.cshtml`):

```html
<!DOCTYPE html>
<html>
<head>
    <title>My App</title>
    <!-- HTMX -->
    <script src="https://unpkg.com/htmx.org@1.9.10"></script>
    <!-- Optional: Include CSS framework like Tailwind or Bootstrap -->
</head>
<body>
    <!-- Navigation Component -->
    @await Component.InvokeAsync("NavBar")
    
    <main>
        @RenderBody()
    </main>
    
    <!-- Authentication Status -->
    @await Component.InvokeAsync("AuthStatus")
    
    <!-- JavaScript Behaviors -->
    <htmx-scripts></htmx-scripts>
    
    <!-- Page State Management -->
    <htmx-page-state></htmx-page-state>
</body>
</html>
```

## Your First Table

Create a simple model and controller to demonstrate table functionality:

```csharp
// Models/User.cs
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// Controllers/UsersController.cs
using Htmx.Components.NavBar;

[NavActionGroup(DisplayName = "User Management")]
public class UsersController : Controller
{
    private readonly ApplicationDbContext _context;

    public UsersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [NavAction(DisplayName = "Users", Icon = "fas fa-users")]
    public async Task<IActionResult> Index()
    {
        var modelHandler = await _modelRegistry.GetModelHandler<User, int>("users", ModelUI.Table);
        var tableModel = await modelHandler.BuildTableModelAndFetchPageAsync();
        return View(tableModel);
    }

    [ModelConfig("users")]
    private void ConfigureUserModel(ModelHandlerBuilder<User, int> builder)
    {
        builder.WithKeySelector(u => u.Id)
               .WithQueryable(() => _context.Users)
               .WithCreate(async user => 
               {
                   _context.Users.Add(user);
                   await _context.SaveChangesAsync();
                   return Result.Value(user);
               })
               .WithTable(table =>
               {
                   table.AddSelectorColumn(u => u.Name);
                   table.AddSelectorColumn(u => u.Email);
                   table.AddSelectorColumn(u => u.CreatedAt);
                   table.AddCrudDisplayColumn();
                   table.WithCrudActions();
               });
    }
}
```

Create the corresponding view (`Views/Users/Index.cshtml`):

```html
@using Htmx.Components.Table.Models
@model ITableModel

<div class="container">
    <h1>Users</h1>
    @await Component.InvokeAsync("Table", Model)
</div>
```

## Next Steps

- [Learn about basic usage patterns](user-guide/basic-usage.md)
- [Set up navigation](user-guide/navigation.md)
- [Configure table features](user-guide/tables.md)
- [Implement authentication](user-guide/authentication.md)
- [Set up authorization](user-guide/authorization.md)

## Troubleshooting

### Common Issues

1. **"PageState not found" error**: Make sure you call `app.UseHtmxPageState()` before authentication middleware.

2. **Navigation not appearing**: Ensure you've registered the navigation provider and included the NavBar component in your layout.

3. **Table not working**: Verify that your model handler is properly configured and the Table component is invoked with the correct model.

4. **Authorization errors**: Check that you've implemented [`IAuthorizationRequirementFactory`](../../api/Htmx.Components.Authorization.IAuthorizationRequirementFactory.html) and [`IResourceOperationRegistry`](../../api/Htmx.Components.Authorization.IResourceOperationRegistry.html).