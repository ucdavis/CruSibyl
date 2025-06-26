# Authentication

HTMX Components provides seamless integration with ASP.NET Core authentication and includes special handling for HTMX requests.

## Basic Authentication Setup

### Standard ASP.NET Core Authentication

HTMX Components works with any ASP.NET Core authentication scheme:

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
    });

// Add HTMX Components after authentication
builder.Services.AddHtmxComponents(options =>
{
    // Configure authentication status provider
    options.WithAuthStatusProvider(sp => new CustomAuthStatusProvider(sp));
});

// Important: Add page state middleware before authentication
app.UseHtmxPageState();
app.UseAuthentication();
app.UseAuthorization();
```

### OIDC/OAuth Integration

For OpenID Connect or OAuth providers:

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(options =>
{
    options.Authority = "https://your-identity-provider.com";
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
    options.ResponseType = "code";
    
    // Configure for HTMX compatibility
    options.ConfigureHtmxAuthPopup("/auth/login-popup");
});
```

## HTMX-Specific Authentication

### Handling Authentication in HTMX Requests

HTMX Components provides special handling for authentication challenges in HTMX requests:

```csharp
public static class OidcOptionsExtensions
{
    public static void ConfigureHtmxAuthPopup(this OpenIdConnectOptions oidc, string popupUrl)
    {
        oidc.Events.OnRedirectToIdentityProvider = context =>
        {
            if (context.Request.IsHtmx())
            {
                // Tell HTMX to show authentication popup instead of redirecting
                context.Response.StatusCode = 401;
                context.Response.Headers["X-Auth-Failure"] = $"popup-login:{popupUrl}";
                context.HandleResponse();
            }
            return Task.CompletedTask;
        };
        
        oidc.Events.OnAccessDenied = context =>
        {
            if (context.Request.IsHtmx())
            {
                context.Response.StatusCode = 403;
                context.Response.Headers["HX-Trigger"] = "auth-denied";
                context.HandleResponse();
            }
            return Task.CompletedTask;
        };
    }
}
```

### Client-Side HTMX Authentication

Handle authentication events on the client side:

```javascript
// Handle authentication failures in HTMX requests
document.addEventListener('htmx:responseError', function(evt) {
    if (evt.detail.xhr.status === 401) {
        const authFailure = evt.detail.xhr.getResponseHeader('X-Auth-Failure');
        if (authFailure && authFailure.startsWith('popup-login:')) {
            const loginUrl = authFailure.substring('popup-login:'.length);
            openAuthPopup(loginUrl);
        }
    }
});

function openAuthPopup(loginUrl) {
    const popup = window.open(loginUrl, 'auth', 'width=500,height=600');
    
    // Listen for successful authentication
    const checkClosed = setInterval(() => {
        if (popup.closed) {
            clearInterval(checkClosed);
            // Refresh the page or trigger a re-authentication check
            location.reload();
        }
    }, 1000);
}
```

## Authentication Status Component

### Default Auth Status Provider

The default provider shows basic authentication information:

```csharp
public class DefaultAuthStatusProvider : IAuthStatusProvider
{
    public Task<AuthStatusViewModel> GetAuthStatusAsync(ClaimsPrincipal user)
    {
        var isAuthenticated = user.Identity?.IsAuthenticated ?? false;
        return Task.FromResult(new AuthStatusViewModel
        {
            IsAuthenticated = isAuthenticated,
            UserName = isAuthenticated ? user.Identity?.Name : null,
            ProfileImageUrl = null,
            LoginUrl = "/Auth/Login"
        });
    }
}
```

### Custom Auth Status Provider

Create a custom provider for enhanced user information:

```csharp
public class CustomAuthStatusProvider : IAuthStatusProvider
{
    private readonly IUserService _userService;

    public CustomAuthStatusProvider(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<AuthStatusViewModel> GetAuthStatusAsync(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new AuthStatusViewModel
            {
                IsAuthenticated = false,
                LoginUrl = "/Auth/Login"
            };
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userProfile = await _userService.GetUserProfileAsync(userId);

        return new AuthStatusViewModel
        {
            IsAuthenticated = true,
            UserName = userProfile?.DisplayName ?? user.Identity.Name,
            ProfileImageUrl = userProfile?.AvatarUrl,
            LoginUrl = null
        };
    }
}

// Register the custom provider
builder.Services.AddHtmxComponents(options =>
{
    options.WithAuthStatusProvider(sp => 
        new CustomAuthStatusProvider(sp.GetRequiredService<IUserService>()));
});
```

### Auth Status View

Display the authentication status in your layout:

```html
<!-- In _Layout.cshtml -->
<div class="auth-container">
    @await Component.InvokeAsync("AuthStatus")
</div>
```

## Authentication Controllers

### Login Controller

Create a controller to handle authentication:

```csharp
public class AuthController : Controller
{
    public IActionResult Login(string returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(returnUrl);
            
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginModel model, string returnUrl = null)
    {
        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, false);
                
            if (result.Succeeded)
            {
                // For HTMX requests, trigger auth status update
                if (Request.IsHtmx())
                {
                    Response.Headers["HX-Trigger"] = "auth-success";
                    return Ok();
                }
                
                return RedirectToLocal(returnUrl);
            }
            
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        
        if (Request.IsHtmx())
        {
            Response.Headers["HX-Trigger"] = "auth-logout";
            return Ok();
        }
        
        return RedirectToAction("Index", "Home");
    }
}
```

### Authentication Filters

Use filters to automatically update authentication status:

```csharp
using Htmx.Components.AuthStatus;

[AuthStatusUpdate]
[HttpPost]
public async Task<IActionResult> Login(LoginModel model)
{
    // Login logic here
    // The AuthStatusUpdate attribute will automatically refresh
    // the auth status component on successful HTMX requests
    return Ok();
}
```

## User Claims and Profiles

### Extending User Claims

Add custom claims during authentication:

```csharp
public async Task<ClaimsIdentity> CreateUserIdentityAsync(User user)
{
    var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
    
    // Standard claims
    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
    identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName));
    identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
    
    // Custom claims
    identity.AddClaim(new Claim("DisplayName", user.DisplayName));
    identity.AddClaim(new Claim("AvatarUrl", user.AvatarUrl ?? ""));
    identity.AddClaim(new Claim("Subscription", user.SubscriptionLevel));
    
    // Add roles
    var roles = await _userManager.GetRolesAsync(user);
    foreach (var role in roles)
    {
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
    }
    
    return identity;
}
```

### User Service Integration

Create a user service for profile management:

```csharp
public interface IUserService
{
    Task<UserProfile> GetUserProfileAsync(string userId);
    Task<bool> UpdateProfileAsync(string userId, UserProfile profile);
}

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserProfile> GetUserProfileAsync(string userId)
    {
        var user = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserProfile
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Email = u.Email,
                AvatarUrl = u.AvatarUrl,
                LastLoginAt = u.LastLoginAt
            })
            .FirstOrDefaultAsync();
            
        return user ?? new UserProfile();
    }
}
```

## Multi-Tenant Authentication

### Tenant Resolution

Implement tenant resolution for multi-tenant applications:

```csharp
public class TenantAuthStatusProvider : IAuthStatusProvider
{
    private readonly ITenantService _tenantService;

    public async Task<AuthStatusViewModel> GetAuthStatusAsync(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return new AuthStatusViewModel { IsAuthenticated = false };

        var tenantId = user.FindFirst("TenantId")?.Value;
        var tenant = await _tenantService.GetTenantAsync(tenantId);

        return new AuthStatusViewModel
        {
            IsAuthenticated = true,
            UserName = $"{user.Identity.Name} ({tenant?.Name})",
            ProfileImageUrl = tenant?.LogoUrl
        };
    }
}
```

## Security Considerations

### CSRF Protection

HTMX Components automatically handles CSRF tokens:

```html
<!-- CSRF tokens are automatically included in HTMX requests -->
<button hx-post="/api/secure-action">Secure Action</button>
```

### Session Security

Configure secure session options:

```csharp
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
```

### Content Security Policy

Configure CSP for HTMX compatibility:

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Content-Security-Policy", 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://unpkg.com; " +
        "style-src 'self' 'unsafe-inline';");
    await next();
});
```

## Best Practices

1. **Use HTTPS**: Always use HTTPS in production
2. **Secure Cookies**: Configure secure cookie settings
3. **Token Validation**: Validate authentication tokens properly
4. **Session Management**: Implement proper session timeout
5. **Audit Logging**: Log authentication events
6. **Rate Limiting**: Implement rate limiting for login attempts

## Troubleshooting

### Authentication Loops

If users get stuck in authentication loops:

1. Check that `UseHtmxPageState()` is called before authentication middleware
2. Verify authentication scheme configuration
3. Ensure proper HTMX headers are being sent

### HTMX Auth Popup Not Working

1. Verify the popup configuration is correct
2. Check that JavaScript event handlers are registered
3. Ensure popup blockers aren't interfering

### Claims Not Available

1. Check that claims are properly added during authentication
2. Verify claim transformations are working
3. Ensure user service is correctly resolving user data