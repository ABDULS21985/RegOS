using System.Security.Claims;
using FC.Engine.Portal.Services;
using FluentAssertions;
using Xunit;

namespace FC.Engine.Portal.Tests.Services;

public class PortalTenantClaimResolverTests
{
    [Theory]
    [InlineData("TenantId")]
    [InlineData("tenant_id")]
    [InlineData("tid")]
    public void ResolveTenantId_Returns_Tenant_For_Supported_Claim_Types(string claimType)
    {
        var tenantId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(claimType, tenantId.ToString())
        ], "test"));

        var resolvedTenantId = PortalTenantClaimResolver.ResolveTenantId(principal);

        resolvedTenantId.Should().Be(tenantId);
    }

    [Fact]
    public void ResolveTenantId_Throws_When_No_Supported_Tenant_Claim_Is_Present()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "demo.user")
        ], "test"));

        var act = () => PortalTenantClaimResolver.ResolveTenantId(principal);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Tenant context is missing from the current session.");
    }
}
