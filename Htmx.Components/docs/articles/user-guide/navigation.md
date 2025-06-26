# Navigation

HTMX Components provides a flexible navigation system that can be configured through attributes or programmatically.

## Navigation Providers

The library supports two types of navigation providers:

1. **Attribute-based Navigation**: Uses controller and action attributes to automatically build navigation
2. **Programmatic Navigation**: Uses a builder pattern to define navigation structure

## Attribute-Based Navigation

The simplest way to create navigation is using attributes on your controllers and actions.

### NavAction Attribute

Mark controller actions that should appear in navigation:

```csharp
using Htmx.Components.NavBar;

[NavAction(DisplayName = "Dashboard", Icon = "fas fa-dashboard", Order = 1)]
public IActionResult Index()
{
    return View();
}

[NavAction(DisplayName = "Users", Icon = "fas fa-users", Order = 2, PushUrl = true)]
public IActionResult Users()
{
    return View();
}
```

### NavActionGroup Attribute

Group related actions together:

```csharp
[NavActionGroup(DisplayName = "Administration", Icon = "fas fa-cog", Order = 100)]
public class AdminController : Controller
{
    [NavAction(DisplayName = "Settings", Icon = "fas fa-gear")]
    public IActionResult Settings() => View();
    
    [NavAction(DisplayName = "Logs", Icon = "fas fa-file-alt")]
    public IActionResult Logs() => View();
}
```

### Attribute Properties

| Property | Description | Default |
|----------|-------------|---------|
| `DisplayName` | Text shown in navigation | Action name (humanized) |
| `Icon` | CSS class for icon | None |
| `Order` | Sort order | 0 |
| `HttpMethod` | HTTP method for HTMX request | "GET" |
| `PushUrl` | Whether to update browser URL | false |
| `ViewName` | View to render for non-HTMX requests | Default action view |

## Programmatic Navigation

For more complex navigation structures, use the programmatic approach:

```csharp
builder.Services.AddHtmxComponents(options =>
{
    options.WithNavBuilder(async nav =>
    {
        // Simple actions
        nav.AddAction(action => action
            .WithLabel("Home")
            .WithIcon("fas fa-home")
            .WithHxGet("/")
            .WithIsActive(IsCurrentPage("/")));
        
        // Grouped actions
        nav.AddGroup(group =>
        {
            group.WithLabel("Management")
                 .WithIcon("fas fa-tools");
            
            group.AddAction(action => action
                .WithLabel("Users")
                .WithIcon("fas fa-users")
                .WithHxGet("/Users"));
                
            group.AddAction(action => action
                .WithLabel("Roles")
                .WithIcon("fas fa-shield")
                .WithHxGet("/Roles"));
        });
        
        // Conditional navigation
        if (user.IsInRole("Admin"))
        {
            nav.AddAction(action => action
                .WithLabel("Admin Panel")
                .WithIcon("fas fa-crown")
                .WithHxGet("/Admin"));
        }
    });
});
```

## Authorization Integration

Navigation items are automatically filtered based on user permissions:

```csharp
[Authorize(Policy = "CanManageUsers")]
[NavAction(DisplayName = "User Management")]
public IActionResult Users() => View();

[Authorize(Roles = "Admin,Manager")]
[NavAction(DisplayName = "Reports")]
public IActionResult Reports() => View();
```

Only navigation items the user is authorized to access will be displayed.

## Custom Navigation Provider

Create your own navigation provider by implementing `INavProvider`:

```csharp
public class DatabaseNavProvider : INavProvider
{
    private readonly IMenuRepository _menuRepository;
    private readonly IServiceProvider _serviceProvider;

    public DatabaseNavProvider(IMenuRepository menuRepository, IServiceProvider serviceProvider)
    {
        _menuRepository = menuRepository;
        _serviceProvider = serviceProvider;
    }

    public async Task<IActionSet> BuildAsync()
    {
        var builder = new ActionSetBuilder(_serviceProvider);
        var menuItems = await _menuRepository.GetMenuItemsAsync();
        
        foreach (var item in menuItems)
        {
            if (item.IsGroup)
            {
                builder.AddGroup(group =>
                {
                    group.WithLabel(item.DisplayName)
                         .WithIcon(item.Icon);
                    
                    foreach (var child in item.Children)
                    {
                        group.AddAction(action => action
                            .WithLabel(child.DisplayName)
                            .WithIcon(child.Icon)
                            .WithHxGet(child.Url));
                    }
                });
            }
            else
            {
                builder.AddAction(action => action
                    .WithLabel(item.DisplayName)
                    .WithIcon(item.Icon)
                    .WithHxGet(item.Url));
            }
        }
        
        return await builder.BuildAsync();
    }
}

// Register the provider
builder.Services.AddScoped<INavProvider, DatabaseNavProvider>();
```

## Navigation Views

### Default Navigation View

The default navigation view renders a horizontal menu with dropdown support:

```html
@await Component.InvokeAsync("NavBar")
```

### Custom Navigation Views

Override navigation rendering by providing a custom view:

```csharp
builder.Services.AddHtmxComponents(options =>
{
    options.WithViewOverrides(views =>
    {
        views.NavBar = "MyCustomNavBar";
    });
});
```

Create `Views/Shared/Components/NavBar/MyCustomNavBar.cshtml`:

```html
@using Htmx.Components.Models
@model IActionSet

<nav class="my-custom-nav">
    @foreach (var item in Model.Items)
    {
        @if (item is ActionModel action)
        {
            <a class="nav-link @(action.IsActive ? "active" : "")"
               @foreach (var attr in action.Attributes)
               {
                   @Html.Raw($"{attr.Key}=\"{attr.Value}\"")
               }>
                @if (!string.IsNullOrEmpty(action.Icon))
                {
                    <i class="@action.Icon"></i>
                }
                @action.Label
            </a>
        }
        @* Handle groups similarly *@
    }
</nav>
```

## Dynamic Navigation

Build navigation dynamically based on user context:

```csharp
public class ContextualNavProvider : INavProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserService _userService;

    public async Task<IActionSet> BuildAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var builder = new ActionSetBuilder(_serviceProvider);
        
        // Always show home
        builder.AddAction(action => action
            .WithLabel("Home")
            .WithHxGet("/"));
        
        if (user?.Identity?.IsAuthenticated == true)
        {
            // Show user-specific navigation
            var userProfile = await _userService.GetProfileAsync(user);
            
            builder.AddAction(action => action
                .WithLabel($"Welcome, {userProfile.Name}")
                .WithHxGet("/Profile"));
            
            if (userProfile.HasSubscription)
            {
                builder.AddAction(action => action
                    .WithLabel("Premium Features")
                    .WithHxGet("/Premium"));
            }
        }
        else
        {
            builder.AddAction(action => action
                .WithLabel("Sign In")
                .WithHxGet("/Auth/Login"));
        }
        
        return await builder.BuildAsync();
    }
}
```

## Navigation State

Navigation automatically tracks the current page and marks active items:

```csharp
[NavAction(DisplayName = "Products")]
public IActionResult Products()
{
    // This action will be marked as active when on /Products
    return View();
}
```

### Custom Active State

Override active state logic:

```csharp
nav.AddAction(action => action
    .WithLabel("Dashboard")
    .WithHxGet("/Dashboard")
    .WithIsActive(IsCurrentSection("Dashboard")));

private bool IsCurrentSection(string section)
{
    var currentPath = _httpContextAccessor.HttpContext?.Request.Path;
    return currentPath?.StartsWithSegments($"/{section}") == true;
}
```

## Best Practices

1. **Use Consistent Icons**: Choose an icon library (like Font Awesome) and use it consistently
2. **Logical Grouping**: Group related functionality together
3. **Clear Labels**: Use descriptive, concise labels
4. **Appropriate Ordering**: Order items by frequency of use or logical flow
5. **Authorization**: Always protect sensitive navigation items with authorization
6. **Responsive Design**: Ensure navigation works on mobile devices

## Troubleshooting

### Navigation Not Appearing

1. Check that `NavBar` component is included in your layout
2. Verify navigation provider is registered
3. Ensure user has permission to view navigation items

### Authorization Not Working

1. Confirm authorization middleware is configured
2. Check that authorization requirements are properly implemented
3. Verify user claims and roles are correct

### Custom Provider Not Loading

1. Ensure provider is registered with DI container
2. Check for exceptions in provider implementation
3. Verify async patterns are used correctly