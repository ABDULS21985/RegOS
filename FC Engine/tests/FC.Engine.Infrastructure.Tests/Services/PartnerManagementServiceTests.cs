using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FC.Engine.Infrastructure.Tests.Services;

public class PartnerManagementServiceTests
{
    private static MetadataDbContext CreateDbContext(string? databaseName = null)
    {
        var dbName = string.IsNullOrWhiteSpace(databaseName) ? Guid.NewGuid().ToString() : databaseName;
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new MetadataDbContext(options);
    }

    private static Tenant CreateTenant(MetadataDbContext db, string slug, TenantType tenantType)
    {
        var tenant = Tenant.Create($"Tenant {slug}", slug, tenantType, $"{slug}@mail.test");
        tenant.Activate();
        db.Tenants.Add(tenant);
        db.SaveChanges();
        return tenant;
    }

    private static PartnerManagementService CreateSut(
        MetadataDbContext db,
        Mock<ITenantOnboardingService>? onboardingMock = null,
        Mock<ITenantBrandingService>? brandingMock = null,
        Mock<ISubscriptionService>? subscriptionMock = null)
    {
        onboardingMock ??= new Mock<ITenantOnboardingService>();
        brandingMock ??= new Mock<ITenantBrandingService>();
        subscriptionMock ??= new Mock<ISubscriptionService>();

        subscriptionMock
            .Setup(x => x.HasFeature(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        return new PartnerManagementService(
            db,
            onboardingMock.Object,
            brandingMock.Object,
            subscriptionMock.Object,
            NullLogger<PartnerManagementService>.Instance);
    }

    [Fact]
    public async Task CreateSubTenant_Throws_When_MaxSubTenant_Limit_Reached()
    {
        using var db = CreateDbContext();

        var partner = CreateTenant(db, "partner-max", TenantType.WhiteLabelPartner);
        var existingSub = CreateTenant(db, "child-max", TenantType.Institution);
        existingSub.SetParentTenant(partner.TenantId);

        db.PartnerConfigs.Add(new PartnerConfig
        {
            TenantId = partner.TenantId,
            PartnerTier = PartnerTier.Silver,
            BillingModel = PartnerBillingModel.Direct,
            CommissionRate = 0.10m,
            MaxSubTenants = 1,
            AgreementSignedAt = DateTime.UtcNow,
            AgreementVersion = "v1"
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var act = () => sut.CreateSubTenant(partner.TenantId, new SubTenantCreateRequest
        {
            TenantName = "New Child",
            ContactEmail = "new@child.test",
            SubscriptionPlanCode = "STARTER",
            AdminEmail = "admin@child.test",
            AdminFullName = "Child Admin",
            InstitutionCode = "CHILD001",
            InstitutionName = "Child Institution"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Sub-tenant limit reached*");
    }

    [Fact]
    public async Task GetSubTenantUsers_Blocks_Access_For_Other_Partner()
    {
        using var db = CreateDbContext();

        var partnerA = CreateTenant(db, "partner-a", TenantType.WhiteLabelPartner);
        var partnerB = CreateTenant(db, "partner-b", TenantType.WhiteLabelPartner);
        var child = CreateTenant(db, "child-a", TenantType.Institution);
        child.SetParentTenant(partnerA.TenantId);

        db.InstitutionUsers.Add(new InstitutionUser
        {
            TenantId = child.TenantId,
            InstitutionId = 1,
            Username = "child.user",
            Email = "child.user@test.local",
            DisplayName = "Child User",
            PasswordHash = "salt:hash",
            Role = InstitutionRole.Maker,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var act = () => sut.GetSubTenantUsers(partnerB.TenantId, child.TenantId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not belong to the current partner*");
    }

    [Fact]
    public async Task OnboardPartner_Uses_Default_Commission_For_Direct_When_Not_Provided()
    {
        using var db = CreateDbContext();

        var partnerTenant = CreateTenant(db, "onboard-partner", TenantType.WhiteLabelPartner);

        var onboardingMock = new Mock<ITenantOnboardingService>();
        onboardingMock
            .Setup(x => x.OnboardTenant(It.IsAny<TenantOnboardingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantOnboardingResult
            {
                Success = true,
                TenantId = partnerTenant.TenantId,
                TenantSlug = partnerTenant.TenantSlug,
                AdminTemporaryPassword = "Temp#123456"
            });

        var sut = CreateSut(db, onboardingMock: onboardingMock);

        var result = await sut.OnboardPartner(new PartnerOnboardingRequest
        {
            TenantName = partnerTenant.TenantName,
            TenantSlug = partnerTenant.TenantSlug,
            ContactEmail = "partner@test.local",
            AdminEmail = "admin@test.local",
            AdminFullName = "Partner Admin",
            PartnerTier = PartnerTier.Silver,
            BillingModel = PartnerBillingModel.Direct,
            AgreementVersion = "v1"
        });

        result.Success.Should().BeTrue();

        var saved = await db.PartnerConfigs.FirstAsync(x => x.TenantId == partnerTenant.TenantId);
        saved.CommissionRate.Should().Be(0.10m);
        saved.BillingModel.Should().Be(PartnerBillingModel.Direct);
        saved.WholesaleDiscount.Should().BeNull();
    }
}
