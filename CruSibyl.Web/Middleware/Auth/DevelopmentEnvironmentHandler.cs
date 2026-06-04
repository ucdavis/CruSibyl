using Microsoft.AspNetCore.Authorization;

namespace CruSibyl.Web.Middleware.Auth;

public sealed class DevelopmentEnvironmentHandler : AuthorizationHandler<DevelopmentEnvironmentRequirement>
{
    private readonly IWebHostEnvironment _environment;

    public DevelopmentEnvironmentHandler(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DevelopmentEnvironmentRequirement requirement)
    {
        if (_environment.IsDevelopment())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
