using Microsoft.AspNetCore.Authorization;

namespace CruSibyl.Web.Middleware.Auth;

public sealed class DevelopmentEnvironmentRequirement : IAuthorizationRequirement
{
}
