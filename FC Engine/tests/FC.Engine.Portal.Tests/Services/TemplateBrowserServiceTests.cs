using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Portal.Services;
using FC.Engine.Portal.Tests.Infrastructure;
using FluentAssertions;
using Moq;
using Xunit;

namespace FC.Engine.Portal.Tests.Services;

public class TemplateBrowserServiceTests
{
    [Fact]
    public async Task GetTemplateDetail_Preserves_Module_Code_For_Module_Aware_Filing()
    {
        var tenantId = Guid.NewGuid();

        var templateCache = new Mock<ITemplateMetadataCache>();
        templateCache
            .Setup(x => x.GetPublishedTemplate("CAP_BUF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedTemplate
            {
                ReturnCode = "CAP_BUF",
                Name = "Capital Buffer Register",
                ModuleCode = "CAPITAL_SUPERVISION",
                Frequency = ReturnFrequency.Monthly,
                StructuralCategory = "FixedRow",
                XmlNamespace = "urn:test:capital",
                XmlRootElement = "CapitalBuffer",
                CurrentVersion = new CachedTemplateVersion
                {
                    Fields =
                    [
                        new TemplateField
                        {
                            Id = 101,
                            FieldName = "capital_buffer",
                            DisplayName = "Capital Buffer",
                            XmlElementName = "CapitalBuffer",
                            DataType = FieldDataType.Decimal,
                            FieldOrder = 1,
                            SectionName = "General"
                        }
                    ]
                }
            });

        var submissionRepo = new Mock<ISubmissionRepository>();
        var xsdGenerator = new Mock<IXsdGenerator>();
        var entitlementService = new Mock<IEntitlementService>();
        entitlementService
            .Setup(x => x.ResolveEntitlements(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantEntitlement
            {
                TenantId = tenantId,
                TenantStatus = TenantStatus.Active
            });

        var fieldLocalisationService = new Mock<IFieldLocalisationService>();
        fieldLocalisationService
            .Setup(x => x.GetLocalisations(It.IsAny<IEnumerable<int>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, FieldLocalisationValue>());

        var languagePreferenceService = new Mock<IUserLanguagePreferenceService>();
        languagePreferenceService
            .Setup(x => x.GetCurrentLanguage(It.IsAny<CancellationToken>()))
            .ReturnsAsync("en");

        var sut = new TemplateBrowserService(
            templateCache.Object,
            submissionRepo.Object,
            xsdGenerator.Object,
            entitlementService.Object,
            new TestTenantContext { CurrentTenantId = tenantId },
            fieldLocalisationService.Object,
            languagePreferenceService.Object);

        var result = await sut.GetTemplateDetail("CAP_BUF");

        result.Should().NotBeNull();
        result!.ModuleCode.Should().Be("CAPITAL_SUPERVISION");
    }
}
