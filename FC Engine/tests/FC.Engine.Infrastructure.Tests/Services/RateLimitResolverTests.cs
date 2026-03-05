using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace FC.Engine.Infrastructure.Tests.Services;

public class RateLimitResolverTests
{
    private readonly Mock<IEntitlementService> _entitlementService = new();
    private readonly RateLimitResolver _sut;

    public RateLimitResolverTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_entitlementService.Object);
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();

        _sut = new RateLimitResolver(
            serviceProvider,
            serviceProvider.GetRequiredService<ILogger<RateLimitResolver>>());
    }

    [Fact]
    public void GetTenantTier_Returns_Default_When_Not_Warmed()
    {
        var result = _sut.GetTenantTier(Guid.NewGuid().ToString());
        result.Should().Be("DEFAULT");
    }

    [Fact]
    public void GetTenantTier_Returns_Default_For_Null_Or_Empty()
    {
        _sut.GetTenantTier("").Should().Be("DEFAULT");
        _sut.GetTenantTier(null!).Should().Be("DEFAULT");
    }

    [Theory]
    [InlineData("STARTER")]
    [InlineData("PROFESSIONAL")]
    [InlineData("ENTERPRISE")]
    [InlineData("GROUP")]
    [InlineData("REGULATOR")]
    [InlineData("WHITE_LABEL")]
    public async Task WarmAsync_Caches_PlanCode_Then_GetTenantTier_Returns_It(string planCode)
    {
        var tenantId = Guid.NewGuid();
        _entitlementService
            .Setup(s => s.ResolveEntitlements(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantEntitlement { TenantId = tenantId, PlanCode = planCode });

        await _sut.WarmAsync(tenantId);

        _sut.GetTenantTier(tenantId.ToString()).Should().Be(planCode);
    }

    [Fact]
    public async Task WarmAsync_Only_Calls_Entitlement_Service_Once_When_Cached()
    {
        var tenantId = Guid.NewGuid();
        _entitlementService
            .Setup(s => s.ResolveEntitlements(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantEntitlement { TenantId = tenantId, PlanCode = "ENTERPRISE" });

        await _sut.WarmAsync(tenantId);
        await _sut.WarmAsync(tenantId);

        _entitlementService.Verify(
            s => s.ResolveEntitlements(tenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WarmAsync_Handles_Exception_Gracefully()
    {
        var tenantId = Guid.NewGuid();
        _entitlementService
            .Setup(s => s.ResolveEntitlements(tenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        // Should not throw
        await _sut.WarmAsync(tenantId);

        _sut.GetTenantTier(tenantId.ToString()).Should().Be("DEFAULT");
    }

    [Fact]
    public async Task WarmAsync_Falls_Back_To_Default_When_PlanCode_Empty()
    {
        var tenantId = Guid.NewGuid();
        _entitlementService
            .Setup(s => s.ResolveEntitlements(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantEntitlement { TenantId = tenantId, PlanCode = "" });

        await _sut.WarmAsync(tenantId);

        _sut.GetTenantTier(tenantId.ToString()).Should().Be("DEFAULT");
    }
}
