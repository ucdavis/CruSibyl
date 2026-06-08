using CruSibyl.Web.Middleware.Auth;

namespace CruSibyl.Tests;

public class PermissionRequirementFactoryTests
{
    [Fact]
    public void ForOperation_CapturesResourceAndOperation()
    {
        var requirement = Assert.IsType<PermissionRequirement>(
            new PermissionRequirementFactory().ForOperation("Repo", "read"));

        Assert.Equal("Repo", requirement.Resource);
        Assert.Equal("read", requirement.Operation);
        Assert.Null(requirement.AllowedRoles);
    }

    [Fact]
    public void ForRoles_CapturesAllowedRoles()
    {
        var requirement = Assert.IsType<PermissionRequirement>(
            new PermissionRequirementFactory().ForRoles("Admin", "System"));

        Assert.Equal(["Admin", "System"], requirement.AllowedRoles);
        Assert.Null(requirement.Resource);
        Assert.Null(requirement.Operation);
    }
}
