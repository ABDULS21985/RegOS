using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Portal.Services;
using FC.Engine.Portal.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FC.Engine.Portal.Tests.Services;

public class DryRunValidationServiceTests
{
    [Fact]
    public async Task GetAvailableTemplatesAsync_Filters_To_Entitled_Module_And_Licence_Access()
    {
        var tenantId = Guid.NewGuid();

        var templateCache = new Mock<ITemplateMetadataCache>();
        templateCache
            .Setup(x => x.GetAllPublishedTemplates(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateTemplate("CAP_BUF", "Capital Buffer Register", moduleCode: "CAPITAL_SUPERVISION"),
                CreateTemplate("BDC_AML", "BDC AML Return", moduleCode: "BDC_CBN"),
                CreateTemplate("LEGACY_BDC", "Legacy BDC Return", institutionType: "BDC"),
                CreateTemplate("DMB_CAP", "DMB Capital Return", moduleCode: "DMB_BASEL3"),
                CreateTemplate("UNSCOPED", "Unscoped Return")
            ]);

        var entitlementService = new Mock<IEntitlementService>();
        entitlementService
            .Setup(x => x.ResolveEntitlements(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantEntitlement
            {
                TenantId = tenantId,
                TenantStatus = TenantStatus.Active,
                LicenceTypeCodes = ["BDC"],
                ActiveModules =
                [
                    new EntitledModule
                    {
                        ModuleCode = "CAPITAL_SUPERVISION",
                        ModuleName = "Capital Supervision",
                        RegulatorCode = "CBN",
                        IsActive = true
                    }
                ]
            });

        var sut = CreateSut(
            templateCache,
            entitlementService,
            new TestTenantContext { CurrentTenantId = tenantId });

        var result = await sut.GetAvailableTemplatesAsync();

        result.Select(x => x.ReturnCode).Should().Equal("CAP_BUF", "LEGACY_BDC");
    }

    [Fact]
    public async Task GetAvailableTemplatesAsync_Returns_Empty_When_Tenant_Context_Is_Missing()
    {
        var templateCache = new Mock<ITemplateMetadataCache>(MockBehavior.Strict);
        var entitlementService = new Mock<IEntitlementService>(MockBehavior.Strict);

        var sut = CreateSut(
            templateCache,
            entitlementService,
            new TestTenantContext());

        var result = await sut.GetAvailableTemplatesAsync();

        result.Should().BeEmpty();
    }

    private static DryRunValidationService CreateSut(
        Mock<ITemplateMetadataCache> templateCache,
        Mock<IEntitlementService> entitlementService,
        ITenantContext tenantContext)
    {
        return new DryRunValidationService(
            templateCache.Object,
            Mock.Of<IXsdGenerator>(),
            Mock.Of<IGenericXmlParser>(),
            Mock.Of<IFormulaEvaluator>(),
            Mock.Of<ICrossSheetValidator>(),
            Mock.Of<IBusinessRuleEvaluator>(),
            Mock.Of<ISubmissionRepository>(),
            NullLogger<DryRunValidationService>.Instance);
    }

    private static CachedTemplate CreateTemplate(
        string returnCode,
        string name,
        string? moduleCode = null,
        string? institutionType = null)
    {
        return new CachedTemplate
        {
            ReturnCode = returnCode,
            Name = name,
            ModuleCode = moduleCode,
            InstitutionType = institutionType ?? string.Empty,
            Frequency = ReturnFrequency.Monthly,
            StructuralCategory = "FixedRow",
            CurrentVersion = new CachedTemplateVersion()
        };
    }
}
