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

public class CalendarServiceTests
{
    [Fact]
    public async Task GetCalendarData_Scopes_To_Entitled_Modules_And_Builds_Module_Aware_Links()
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
                        ModuleId = 11,
                        ModuleCode = "CAPITAL_SUPERVISION",
                        ModuleName = "Capital Supervision",
                        RegulatorCode = "CBN",
                        DefaultFrequency = "Monthly",
                        IsActive = true,
                        SheetCount = 6
                    }
                ]
            });

        var templateCache = new Mock<ITemplateMetadataCache>();
        templateCache
            .Setup(x => x.GetAllPublishedTemplates(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateTemplate("CAP_BUF", "Capital Buffer Register", "CAPITAL_SUPERVISION", 11, ReturnFrequency.Monthly),
                CreateTemplate("MRM_INV", "Model Inventory", "MODEL_RISK", 22, ReturnFrequency.Monthly)
            ]);

        var submissionRepository = new Mock<ISubmissionRepository>();
        submissionRepository
            .Setup(x => x.GetByInstitution(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Submission>());

        var sut = new CalendarService(
            templateCache.Object,
            submissionRepository.Object,
            entitlementService.Object,
            new TestTenantContext { CurrentTenantId = tenantId });

        var result = await sut.GetCalendarData(
            44,
            new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
            new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1).AddDays(-1));

        result.Entries.Should().NotBeEmpty();
        result.Entries.Should().OnlyContain(entry => entry.ReturnCode == "CAP_BUF");
        result.Entries.Should().OnlyContain(entry => entry.ModuleCode == "CAPITAL_SUPERVISION");
        result.Entries.Should().OnlyContain(entry => entry.ModuleName == "Capital Supervision");
        result.Entries.Should().OnlyContain(entry => entry.StartHref == "/submit?module=CAPITAL_SUPERVISION&returnCode=CAP_BUF");
        result.Entries.Should().OnlyContain(entry => entry.WorkspaceHref == "/workflows/capital-supervision");
    }

    private static CachedTemplate CreateTemplate(
        string returnCode,
        string name,
        string moduleCode,
        int moduleId,
        ReturnFrequency frequency) =>
        new()
        {
            ReturnCode = returnCode,
            Name = name,
            ModuleCode = moduleCode,
            ModuleId = moduleId,
            Frequency = frequency,
            StructuralCategory = "FixedRow",
            CurrentVersion = new CachedTemplateVersion()
        };
}
