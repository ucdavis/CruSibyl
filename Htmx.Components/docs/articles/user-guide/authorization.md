# Authorization

HTMX Components provides a comprehensive authorization system that integrates seamlessly with ASP.NET Core's authorization framework while adding resource-operation based permissions.

## Core Concepts

The authorization system is built around three key concepts:

1. **Resources**: The entities or data types being protected (e.g., "users", "orders", "reports")
2. **Operations**: The actions being performed (e.g., "read", "create", "update", "delete")
3. **Requirements**: The authorization logic that determines if a user can perform an operation on a resource

## Permission Requirement Factory

### Basic Implementation

Implement [`IAuthorizationRequirementFactory`](../../api/Htmx.Components.Authorization.IAuthorizationRequirementFactory.html) to create authorization requirements:

```csharp
public class SimpleAuthorizationRequirementFactory : IAuthorizationRequirementFactory
{
    public IAuthorizationRequirement ForOperation(string resource, string operation)
    {
        return new OperationAuthorizationRequirement 
        { 
            Name = $"{resource}:{operation}" 
        };
    }

    public IAuthorizationRequirement ForRoles(params string[] roles)
    {
        return new RolesAuthorizationRequirement(roles);
    }
}

// Register the factory
builder.Services.AddHtmxComponents(options =>
{
    options.WithAuthorizationRequirementFactory<SimpleAuthorizationRequirementFactory>();
});
```

### Advanced Implementation

Create more sophisticated authorization logic:

```csharp
public class AdvancedAuthorizationRequirementFactory : IAuthorizationRequirementFactory
{
    public IAuthorizationRequirement ForOperation(string resource, string operation)
    {
        // Create resource-specific requirements
        return resource switch
        {
            "users" => new UserManagementRequirement(operation),
            "orders" => new OrderAccessRequirement(operation),
            "reports" => new ReportsRequirement(operation),
            _ => new GenericResourceRequirement(resource, operation)
        };
    }

    public IAuthorizationRequirement ForRoles(params string[] roles)
    {
        return new RolesAuthorizationRequirement(roles);
    }
}

// Custom authorization requirements
public class UserManagementRequirement : IAuthorizationRequirement
{
    public string Operation { get; }
    public UserManagementRequirement(string operation) => Operation = operation;
}

public class OrderAccessRequirement : IAuthorizationRequirement
{
    public string Operation { get; }
    public OrderAccessRequirement(string operation) => Operation = operation;
}
```

## Resource Operation Registry

### Basic Registry

Implement [`IResourceOperationRegistry`](../../api/Htmx.Components.Authorization.IResourceOperationRegistry.html) to track available resource-operations:

```csharp
public class InMemoryResourceOperationRegistry : IResourceOperationRegistry
{
    private readonly HashSet<string> _registeredOperations = new();

    public Task Register(string resource, string operation)
    {
        _registeredOperations.Add($"{resource}:{operation}");
        Console.WriteLine($"Registered operation: {resource}:{operation}");
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetRegisteredOperations() => _registeredOperations;
}
```

### Database-Backed Registry

Store resource-operations in a database:

```csharp
public class DatabaseResourceOperationRegistry : IResourceOperationRegistry
{
    private readonly ApplicationDbContext _context;

    public DatabaseResourceOperationRegistry(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Register(string resource, string operation)
    {
        var exists = await _context.ResourceOperations
            .AnyAsync(ro => ro.Resource == resource && ro.Operation == operation);
            
        if (!exists)
        {
            _context.ResourceOperations.Add(new ResourceOperation
            {
                Resource = resource,
                Operation = operation,
                CreatedAt = DateTime.UtcNow
            });
            
            await _context.SaveChangesAsync();
        }
    }
}

// Entity model
public class ResourceOperation
{
    public int Id { get; set; }
    public string Resource { get; set; } = "";
    public string Operation { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
```

## Authorization Handlers

### Custom Authorization Handlers

Create handlers for your custom requirements:

```csharp
public class UserManagementHandler : AuthorizationHandler<UserManagementRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, 
        UserManagementRequirement requirement)
    {
        var user = context.User;
        
        // Check if user has admin role
        if (user.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
        
        // Check specific permissions based on operation
        var hasPermission = requirement.Operation switch
        {
            "read" => user.HasClaim("permission", "users:read") || 
                     user.IsInRole("UserViewer"),
            "create" => user.HasClaim("permission", "users:create") || 
                       user.IsInRole("UserManager"),
            "update" => user.HasClaim("permission", "users:update") || 
                       user.IsInRole("UserManager"),
            "delete" => user.HasClaim("permission", "users:delete") || 
                       user.IsInRole("UserAdmin"),
            _ => false
        };
        
        if (hasPermission)
            context.Succeed(requirement);
            
        return Task.CompletedTask;
    }
}

// Register the handler
builder.Services.AddScoped<IAuthorizationHandler, UserManagementHandler>();
```

### Resource-Specific Handlers

Create handlers that work with specific resources:

```csharp
public class OrderAccessHandler : AuthorizationHandler<OrderAccessRequirement, Order>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrderAccessHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrderAccessRequirement requirement,
        Order resource)
    {
        var user = context.User;
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Users can always access their own orders
        if (resource.CustomerId == userId)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
        
        // Admins can access all orders
        if (user.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
        
        // Sales team can read all orders
        if (requirement.Operation == "read" && user.IsInRole("Sales"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
        
        return Task.CompletedTask;
    }
}
```

## Role-Based Authorization

### Role Service

Implement [`IRoleService`](../../api/Htmx.Components.Services.IRoleService.html) for role-based authorization:

```csharp
public class DatabaseRoleService : IRoleService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public DatabaseRoleService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<bool> UserHasAnyRoleAsync(ClaimsPrincipal user, IEnumerable<string> requiredRoles)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return false;
            
        var appUser = await _userManager.FindByIdAsync(userId);
        if (appUser == null)
            return false;
            
        var userRoles = await _userManager.GetRolesAsync(appUser);
        return requiredRoles.Any(role => userRoles.Contains(role));
    }
}

// Register the service
builder.Services.AddHtmxComponents(options =>
{
    options.WithRoleService<DatabaseRoleService>();
});
```

### Custom Role Provider

Create a more sophisticated role system:

```csharp
public class HierarchicalRoleService : IRoleService
{
    private readonly IUserService _userService;
    private readonly IRoleHierarchyService _roleHierarchyService;

    public async Task<bool> UserHasAnyRoleAsync(ClaimsPrincipal user, IEnumerable<string> requiredRoles)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRoles = await _userService.GetUserRolesAsync(userId);
        
        // Check direct role membership
        if (requiredRoles.Any(role => userRoles.Contains(role)))
            return true;
            
        // Check role hierarchy (e.g., Admin includes Manager includes User)
        var expandedRoles = await _roleHierarchyService.ExpandRolesAsync(userRoles);
        return requiredRoles.Any(role => expandedRoles.Contains(role));
    }
}
```

## Model Handler Authorization

### Automatic Authorization

Model handlers automatically register their operations:

```csharp
[ModelConfig("products")]
private void ConfigureProductModel(ModelHandlerBuilder<Product, int> builder)
{
    builder.WithKeySelector(p => p.Id)
           .WithQueryable(() => _context.Products)
           .WithCreate(CreateProduct)    // Registers "products:create"
           .WithUpdate(UpdateProduct)    // Registers "products:update"  
           .WithDelete(DeleteProduct);   // Registers "products:delete"
                                        // Read is registered with WithQueryable
}
```

### Manual Authorization Checks

Perform authorization checks in controllers:

```csharp
public async Task<IActionResult> SpecialReport()
{
    var requirement = _permissionFactory.ForOperation("reports", "special");
    var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
    
    if (!authResult.Succeeded)
        return Forbid();
        
    // Generate report
    var report = await _reportService.GenerateSpecialReportAsync();
    return View(report);
}
```

## Navigation Authorization

### Attribute-Based Authorization

Navigation items are automatically filtered based on authorization:

```csharp
using Htmx.Components.NavBar;

[Authorize(Policy = "CanManageUsers")]
[NavAction(DisplayName = "User Management")]
public IActionResult Users() => View();

[Authorize(Roles = "Admin")]
[NavAction(DisplayName = "System Settings")]
public IActionResult Settings() => View();
```

### Programmatic Authorization

Check authorization in programmatic navigation:

```csharp
builder.Services.AddHtmxComponents(options =>
{
    options.WithNavBuilder(async nav =>
    {
        // Always show home
        nav.AddAction(action => action
            .WithLabel("Home")
            .WithHxGet("/"));
        
        // Check if user can manage users
        var userManagementReq = permissionFactory.ForOperation("users", "read");
        var canManageUsers = await authService.AuthorizeAsync(user, userManagementReq);
        
        if (canManageUsers.Succeeded)
        {
            nav.AddAction(action => action
                .WithLabel("Users")
                .WithHxGet("/Users"));
        }
    });
});
```

## Claims-Based Authorization

### Custom Claims

Add custom claims during authentication:

```csharp
public async Task<ClaimsIdentity> CreateClaimsIdentityAsync(User user)
{
    var identity = new ClaimsIdentity();
    
    // Basic claims
    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
    identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName));
    
    // Permission claims
    var permissions = await _permissionService.GetUserPermissionsAsync(user.Id);
    foreach (var permission in permissions)
    {
        identity.AddClaim(new Claim("permission", permission));
    }
    
    // Department claim
    if (!string.IsNullOrEmpty(user.Department))
    {
        identity.AddClaim(new Claim("department", user.Department));
    }
    
    return identity;
}
```

### Claims-Based Requirements

Create requirements that check specific claims:

```csharp
public class DepartmentAccessRequirement : IAuthorizationRequirement
{
    public string Department { get; }
    public DepartmentAccessRequirement(string department) => Department = department;
}

public class DepartmentAccessHandler : AuthorizationHandler<DepartmentAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DepartmentAccessRequirement requirement)
    {
        var userDepartment = context.User.FindFirst("department")?.Value;
        
        if (userDepartment == requirement.Department || 
            context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
        }
        
        return Task.CompletedTask;
    }
}
```

## Multi-Tenant Authorization

### Tenant-Aware Authorization

Implement authorization that considers tenant context:

```csharp
public class TenantAwarePermissionFactory : IAuthorizationRequirementFactory
{
    public IAuthorizationRequirement ForOperation(string resource, string operation)
    {
        return new TenantResourceRequirement(resource, operation);
    }

    public IAuthorizationRequirement ForRoles(params string[] roles)
    {
        return new TenantRolesRequirement(roles);
    }
}

public class TenantResourceRequirement : IAuthorizationRequirement
{
    public string Resource { get; }
    public string Operation { get; }
    
    public TenantResourceRequirement(string resource, string operation)
    {
        Resource = resource;
        Operation = operation;
    }
}

public class TenantResourceHandler : AuthorizationHandler<TenantResourceRequirement>
{
    private readonly ITenantService _tenantService;

    public TenantResourceHandler(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantResourceRequirement requirement)
    {
        var tenantId = await _tenantService.GetCurrentTenantIdAsync();
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Check if user belongs to the current tenant
        var userTenant = context.User.FindFirst("tenant")?.Value;
        if (userTenant != tenantId)
        {
            return; // User doesn't belong to current tenant
        }
        
        // Check tenant-specific permissions
        var hasPermission = await _tenantService.UserHasPermissionAsync(
            userId, tenantId, requirement.Resource, requirement.Operation);
            
        if (hasPermission)
            context.Succeed(requirement);
    }
}
```

## Best Practices

### 1. Principle of Least Privilege

Grant users only the minimum permissions necessary:

```csharp
// Instead of blanket admin access
[Authorize(Roles = "Admin")]

// Use specific permissions
[Authorize(Policy = "CanManageUsers")]
public IActionResult Users() => View();
```

### 2. Separate Read and Write Operations

Create distinct permissions for different operations:

```csharp
[ModelConfig("orders")]
private void ConfigureOrderModel(ModelHandlerBuilder<Order, int> builder)
{
    builder.WithQueryable(() => _context.Orders)      // orders:read
           .WithCreate(CreateOrder)                    // orders:create
           .WithUpdate(UpdateOrder)                    // orders:update
           .WithDelete(DeleteOrder);                   // orders:delete
}
```

### 3. Use Authorization Policies

Define policies for complex authorization logic:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageUsers", policy =>
        policy.RequireRole("Admin", "UserManager")
              .RequireClaim("department", "HR", "IT"));

    options.AddPolicy("CanViewReports", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new ReportAccessRequirement()));
});
```

### 4. Cache Authorization Results

Use caching for expensive authorization operations:

```csharp
public class CachedAuthorizationService : IAuthorizationService
{
    private readonly IAuthorizationService _inner;
    private readonly IMemoryCache _cache;

    public async Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user, 
        object resource, 
        IAuthorizationRequirement requirement)
    {
        var cacheKey = $"auth:{user.FindFirst(ClaimTypes.NameIdentifier)?.Value}:{requirement}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await _inner.AuthorizeAsync(user, resource, requirement);
        });
    }
}
```

### 5. Audit Authorization Decisions

Log authorization successes and failures:

```csharp
public class AuditingAuthorizationHandler : AuthorizationHandler<IAuthorizationRequirement>
{
    private readonly ILogger<AuditingAuthorizationHandler> _logger;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IAuthorizationRequirement requirement)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (context.HasSucceeded)
        {
            _logger.LogInformation(
                "Authorization succeeded for user {UserId} with requirement {Requirement}",
                userId, requirement.GetType().Name);
        }
        else
        {
            _logger.LogWarning(
                "Authorization failed for user {UserId} with requirement {Requirement}",
                userId, requirement.GetType().Name);
        }
        
        return Task.CompletedTask;
    }
}
```

## Testing Authorization

### Unit Testing Requirements

```csharp
[Test]
public async Task UserManagementHandler_AdminUser_ShouldSucceed()
{
    // Arrange
    var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.Role, "Admin")
    }));
    
    var context = new AuthorizationHandlerContext(
        new[] { new UserManagementRequirement("read") }, user, null);
    
    var handler = new UserManagementHandler();
    
    // Act
    await handler.HandleAsync(context);
    
    // Assert
    Assert.IsTrue(context.HasSucceeded);
}
```

### Integration Testing

```csharp
[Test]
public async Task GetUsers_UnauthorizedUser_ShouldReturnForbidden()
{
    // Arrange
    var client = _factory.CreateClient();
    
    // Act
    var response = await client.GetAsync("/Users");
    
    // Assert
    Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
}
```

This comprehensive authorization system provides fine-grained control over access to resources while maintaining flexibility and performance. The system integrates seamlessly with ASP.NET Core's built-in authorization framework while adding powerful resource-operation based permissions specifically designed for HTMX applications.