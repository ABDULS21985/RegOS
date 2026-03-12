using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Portal.Services;
using FC.Engine.Portal.Tests.Infrastructure;
using FluentAssertions;
using Moq;
using Xunit;

namespace FC.Engine.Portal.Tests.Services;

public class PortalSubmissionLaunchServiceTests
{
    [Fact]
    public async Task ResolvePrimarySubmitAsync_Picks_Entitled_Module_And_First_Return()
    {
        var tenantId = Guid.NewGuid();
        var entitlementService = new Mock<IEntitlementService>();
        entitlementService
            .Setup(x => x.ResolveEntitlements(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantEntitlement
            {
                TenantId = tenantId,
                TenantStatus = TenantStatus.Active,
                ActiveModules =
                [
                    new EntitledModule
                    {
                        ModuleId = 22,
                        ModuleCode = "MODEL_RISK",
                        ModuleName = "Model Risk",
                        RegulatorCode = "CBN",
                        DefaultFrequency = "Monthly",
                        IsActive = true,
                        SheetCount = 9
                    }
                ]
            });

        var templateCache = new Mock<ITemplateMetadataCache>();
        templateCache
            .Setup(x => x.GetAllPublishedTemplates(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateTemplate("MRM_VAL", "Validation Status", "MODEL_RISK", 22),
                CreateTemplate("CAP_BUF", "Capital Buffer Register", "CAPITAL_SUPERVISION", 11)
            ]);

        var sut = new PortalSubmissionLaunchService(
            new TestTenantContext { CurrentTenantId = tenantId },
            entitlementService.Object,
            templateCache.Object);

        var result = await sut.ResolvePrimarySubmitAsync();

        result.Href.Should().Be("/submit?module=MODEL_RISK&returnCode=MRM_VAL");
        result.ModuleCode.Should().Be("MODEL_RISK");
        result.ModuleName.Should().Be("Model Risk");
        result.WorkspaceHref.Should().Be("/workflows/model-risk");
    }

    [Fact]
    public async Task ResolvePrimarySubmitAsync_Falls_Back_To_Module_When_No_Template_Is_Available()
    {
        var tenantId = Guid.NewGuid();
        var entitlementService = new Mock<IEntitlementService>();
        entitlementService
            .Setup(x => x.ResolveEntitlements(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantEntitlement
            {
                TenantId = tenantId,
                TenantStatus = TenantStatus.Active,
                ActiveModules =
                [
                    new EntitledModule
                    {
                        ModuleId = 33,
                        ModuleCode = "OPS_RESILIENCE",
                        ModuleName = "Operational Resilience",
                        RegulatorCode = "CBN",
                        DefaultFrequency = "Quarterly",
                        IsActive = true,
                        SheetCount = 10
                    }
                ]
            });

        var templateCache = new Mock<ITemplateMetadataCache>();
        templateCache
            .Setup(x => x.GetAllPublishedTemplates(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CachedTemplate>());

        var sut = new PortalSubmissionLaunchService(
            new TestTenantContext { CurrentTenantId = tenantId },
            entitlementService.Object,
            templateCache.Object);

        var result = await sut.ResolvePrimarySubmitAsync();

        result.Href.Should().Be("/submit?module=OPS_RESILIENCE");
        result.ModuleCode.Should().Be("OPS_RESILIENCE");
        result.WorkspaceHref.Should().Be("/workflows/ops-resilience");
    }

    [Fact]
    public async Task ResolvePrimarySubmitAsync_Falls_Back_When_Tenant_Context_Is_Missing()
    {
        var entitlementService = new Mock<IEntitlementService>(MockBehavior.Strict);
        var templateCache = new Mock<ITemplateMetadataCache>(MockBehavior.Strict);

        var sut = new PortalSubmissionLaunchService(
            new TestTenantContext(),
            entitlementService.Object,
            templateCache.Object);

        var result = await sut.ResolvePrimarySubmitAsync();

        result.Href.Should().Be("/submit");
        result.ModuleCode.Should().BeNull();
        result.ReturnCode.Should().BeNull();
    }

    private static CachedTemplate CreateTemplate(string returnCode, string name, string moduleCode, int moduleId) =>
        new()
        {
            ReturnCode = returnCode,
            Name = name,
            ModuleCode = moduleCode,
            ModuleId = moduleId,
            Frequency = ReturnFrequency.Monthly,
            StructuralCategory = "FixedRow",
            CurrentVersion = new CachedTemplateVersion()
        };
}
