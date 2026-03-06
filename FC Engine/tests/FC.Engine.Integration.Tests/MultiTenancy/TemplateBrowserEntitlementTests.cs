using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Portal.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace FC.Engine.Integration.Tests.MultiTenancy;

public class TemplateBrowserEntitlementTests
{
    [Fact]
    public async Task GetAllTemplates_Filters_By_Active_Module_Entitlements()
    {
        var tenantId = Guid.NewGuid();

        var cache = new Mock<ITemplateMetadataCache>();
        cache.Setup(c => c.GetAllPublishedTemplates(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CachedTemplate>
            {
                CreateCachedTemplate("FC_COV", null),
                CreateCachedTemplate("BDC_COV", "BDC_CBN"),
                CreateCachedTemplate("MFB_COV", "MFB_PAR")
            });

        var submissionRepo = new Mock<ISubmissionRepository>();
        var xsdGenerator = new Mock<IXsdGenerator>();
        var entitlement = new Mock<IEntitlementService>();
        var localisationService = new Mock<IFieldLocalisationService>();
        localisationService
            .Setup(x => x.GetLocalisations(It.IsAny<IEnumerable<int>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, FieldLocalisationValue>());
        var languagePreference = new Mock<IUserLanguagePreferenceService>();
        languagePreference
            .Setup(x => x.GetCurrentLanguage(It.IsAny<CancellationToken>()))
            .ReturnsAsync("en");
        entitlement.Setup(e => e.ResolveEntitlements(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantEntitlement
            {
                TenantId = tenantId,
                TenantStatus = TenantStatus.Active,
                ActiveModules = new[]
                {
                    new EntitledModule
                    {
                        ModuleCode = "BDC_CBN",
                        ModuleName = "BDC Module",
                        RegulatorCode = "CBN",
                        IsActive = true
                    }
                },
                ResolvedAt = DateTime.UtcNow
            });

        var tenantContext = new StubTenantContext(tenantId);
        var sut = new TemplateBrowserService(
            cache.Object,
            submissionRepo.Object,
            xsdGenerator.Object,
            entitlement.Object,
            tenantContext,
            localisationService.Object,
            languagePreference.Object);

        var templates = await sut.GetAllTemplates();

        templates.Select(t => t.ReturnCode).Should().Contain("FC_COV");
        templates.Select(t => t.ReturnCode).Should().Contain("BDC_COV");
        templates.Select(t => t.ReturnCode).Should().NotContain("MFB_COV");
    }

    [Fact]
    public async Task GetTemplateDetail_Applies_Field_Localisation_For_User_Language()
    {
        var cache = new Mock<ITemplateMetadataCache>();
        cache.Setup(c => c.GetPublishedTemplate("FC_COV", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCachedTemplate("FC_COV", "FC_RETURNS"));

        var submissionRepo = new Mock<ISubmissionRepository>();
        var xsdGenerator = new Mock<IXsdGenerator>();
        var entitlement = new Mock<IEntitlementService>();
        var tenantContext = new StubTenantContext(Guid.NewGuid());
        var localisationService = new Mock<IFieldLocalisationService>();
        localisationService
            .Setup(x => x.GetLocalisations(It.IsAny<IEnumerable<int>>(), "fr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, FieldLocalisationValue>
            {
                [10] = new() { Label = "Nom localise", HelpText = "Texte d'aide" }
            });
        var languagePreference = new Mock<IUserLanguagePreferenceService>();
        languagePreference
            .Setup(x => x.GetCurrentLanguage(It.IsAny<CancellationToken>()))
            .ReturnsAsync("fr");

        var sut = new TemplateBrowserService(
            cache.Object,
            submissionRepo.Object,
            xsdGenerator.Object,
            entitlement.Object,
            tenantContext,
            localisationService.Object,
            languagePreference.Object);

        var detail = await sut.GetTemplateDetail("FC_COV");

        detail.Should().NotBeNull();
        detail!.Fields.Should().ContainSingle();
        detail.Fields[0].DisplayName.Should().Be("Nom localise");
        detail.Fields[0].HelpText.Should().Be("Texte d'aide");
    }

    private static CachedTemplate CreateCachedTemplate(string returnCode, string? moduleCode)
    {
        return new CachedTemplate
        {
            TemplateId = Random.Shared.Next(1, 5000),
            ReturnCode = returnCode,
            Name = $"{returnCode} Template",
            Frequency = ReturnFrequency.Monthly,
            StructuralCategory = StructuralCategory.FixedRow.ToString(),
            PhysicalTableName = $"t_{returnCode.ToLowerInvariant()}",
            XmlRootElement = returnCode,
            XmlNamespace = $"urn:regos:{returnCode.ToLowerInvariant()}",
            ModuleCode = moduleCode,
            CurrentVersion = new CachedTemplateVersion
            {
                Id = Random.Shared.Next(1, 5000),
                VersionNumber = 1,
                Fields = new List<TemplateField>
                {
                    new()
                    {
                        Id = 10,
                        FieldName = "sample",
                        DisplayName = "Sample",
                        XmlElementName = "Sample",
                        FieldOrder = 1,
                        DataType = FieldDataType.Text
                    }
                },
                IntraSheetFormulas = Array.Empty<IntraSheetFormula>(),
                ItemCodes = Array.Empty<TemplateItemCode>()
            }
        };
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public StubTenantContext(Guid tenantId)
        {
            CurrentTenantId = tenantId;
        }

        public Guid? CurrentTenantId { get; }
        public bool IsPlatformAdmin => false;
        public Guid? ImpersonatingTenantId => null;
    }
}
