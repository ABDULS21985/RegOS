using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Portal.Services;
using FC.Engine.Portal.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace FC.Engine.Portal.Tests.Services;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetDashboardDataAsync_Scopes_To_Active_Entitled_Modules_And_Builds_Workspace_Links()
    {
        var tenantId = Guid.NewGuid();
        var cache = new MemoryCache(new MemoryCacheOptions());

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
                ],
                EligibleModules =
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
                CreateTemplate("MRM_INV", "Model Inventory", "MODEL_RISK", 22, ReturnFrequency.Quarterly)
            ]);

        var submissionRepository = new Mock<ISubmissionRepository>();
        submissionRepository
            .Setup(x => x.GetByInstitution(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateSubmission(tenantId, 44, 5001, "CAP_BUF", SubmissionStatus.Accepted, DateTime.UtcNow.AddDays(-2)),
                CreateSubmission(tenantId, 44, 5002, "MRM_INV", SubmissionStatus.Accepted, DateTime.UtcNow.AddDays(-1))
            ]);

        var sut = new DashboardService(
            submissionRepository.Object,
            templateCache.Object,
            entitlementService.Object,
            new TestTenantContext { CurrentTenantId = tenantId },
            cache);

        var result = await sut.GetDashboardDataAsync(44, "Sample Capital Institution", "SCI");

        result.DueThisMonth.Should().Be(1);
        result.TotalReturnsDue.Should().Be(1);
        result.RecentSubmissions.Should().ContainSingle();
        result.RecentSubmissions[0].ReturnCode.Should().Be("CAP_BUF");
        result.RecentSubmissions[0].ModuleCode.Should().Be("CAPITAL_SUPERVISION");
        result.UpcomingDeadlines.Should().ContainSingle();
        result.UpcomingDeadlines[0].StartHref.Should().Be("/submit?module=CAPITAL_SUPERVISION&returnCode=CAP_BUF");
        result.TopModules.Should().ContainSingle();
        result.TopModules[0].WorkspaceHref.Should().Be("/workflows/capital-supervision");
        result.ModuleWorkspaces.Should().ContainSingle();
        result.ModuleWorkspaces[0].ModuleCode.Should().Be("CAPITAL_SUPERVISION");
        result.ModuleWorkspaces[0].WorkspaceHref.Should().Be("/workflows/capital-supervision");
    }

    [Fact]
    public async Task GetComplianceDashboardAsync_Uses_Entitled_Template_Set()
    {
        var tenantId = Guid.NewGuid();
        var cache = new MemoryCache(new MemoryCacheOptions());

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
                        ModuleId = 31,
                        ModuleCode = "OPS_RESILIENCE",
                        ModuleName = "Operational Resilience",
                        RegulatorCode = "CBN",
                        DefaultFrequency = "Monthly",
                        IsActive = true,
                        SheetCount = 10
                    }
                ],
                EligibleModules =
                [
                    new EntitledModule
                    {
                        ModuleId = 31,
                        ModuleCode = "OPS_RESILIENCE",
                        ModuleName = "Operational Resilience",
                        RegulatorCode = "CBN",
                        DefaultFrequency = "Monthly",
                        IsActive = true,
                        SheetCount = 10
                    }
                ]
            });

        var templateCache = new Mock<ITemplateMetadataCache>();
        templateCache
            .Setup(x => x.GetAllPublishedTemplates(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateTemplate("OPS_IBS", "Important Business Services", "OPS_RESILIENCE", 31, ReturnFrequency.Monthly),
                CreateTemplate("MRM_INV", "Model Inventory", "MODEL_RISK", 22, ReturnFrequency.Quarterly)
            ]);

        var submissionRepository = new Mock<ISubmissionRepository>();
        submissionRepository
            .Setup(x => x.GetByInstitution(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateSubmission(tenantId, 9, 7001, "OPS_IBS", SubmissionStatus.Accepted, DateTime.UtcNow.AddDays(-3)),
                CreateSubmission(tenantId, 9, 7002, "MRM_INV", SubmissionStatus.Accepted, DateTime.UtcNow.AddDays(-2))
            ]);

        var sut = new DashboardService(
            submissionRepository.Object,
            templateCache.Object,
            entitlementService.Object,
            new TestTenantContext { CurrentTenantId = tenantId },
            cache);

        var result = await sut.GetComplianceDashboardAsync(9, "Resilience Bank", "RB");

        result.AvailableReturnCodes.Should().Equal("OPS_IBS");
        result.ModuleBreakdowns.Should().ContainSingle(x => x.ReturnCode == "OPS_IBS");
        result.ModuleBreakdowns.Should().NotContain(x => x.ReturnCode == "MRM_INV");
    }

    [Fact]
    public async Task GetNavBadgeCountsAsync_Scopes_To_Entitled_Modules_And_Builds_ModuleAware_Quick_Submit_Items()
    {
        var tenantId = Guid.NewGuid();
        var cache = new MemoryCache(new MemoryCacheOptions());

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

        var now = DateTime.UtcNow;
        var lastMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        var submissionRepository = new Mock<ISubmissionRepository>();
        submissionRepository
            .Setup(x => x.GetByInstitution(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateSubmission(tenantId, 44, 8001, "CAP_BUF", SubmissionStatus.PendingApproval, currentMonth.AddDays(2)),
                CreateSubmission(tenantId, 44, 8002, "MRM_INV", SubmissionStatus.PendingApproval, lastMonth.AddDays(5))
            ]);

        var sut = new DashboardService(
            submissionRepository.Object,
            templateCache.Object,
            entitlementService.Object,
            new TestTenantContext { CurrentTenantId = tenantId },
            cache);

        var result = await sut.GetNavBadgeCountsAsync(44);

        result.PendingApprovalCount.Should().Be(1);
        result.OverdueCount.Should().BeGreaterThan(0);
        result.RecentReturnActions.Should().ContainSingle();
        result.RecentReturnActions[0].ReturnCode.Should().Be("CAP_BUF");
        result.RecentReturnActions[0].ModuleCode.Should().Be("CAPITAL_SUPERVISION");
        result.RecentReturnActions[0].Href.Should().Be("/submit?module=CAPITAL_SUPERVISION&returnCode=CAP_BUF");
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

    private static Submission CreateSubmission(
        Guid tenantId,
        int institutionId,
        int submissionId,
        string returnCode,
        SubmissionStatus status,
        DateTime submittedAt) =>
        new()
        {
            Id = submissionId,
            TenantId = tenantId,
            InstitutionId = institutionId,
            ReturnCode = returnCode,
            ReturnPeriodId = submissionId,
            Status = status,
            SubmittedAt = submittedAt,
            CreatedAt = submittedAt
        };
}
