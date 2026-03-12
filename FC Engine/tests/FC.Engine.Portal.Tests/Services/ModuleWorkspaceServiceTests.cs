using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Domain.Models;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Portal.Services;
using FC.Engine.Portal.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FC.Engine.Portal.Tests.Services;

public class ModuleWorkspaceServiceTests
{
    [Fact]
    public async Task GetWorkspaceAsync_Builds_Attention_Queue_With_Module_Aware_Actions()
    {
        var tenantId = Guid.NewGuid();
        var dbFactory = new TestMetadataDbContextFactory(nameof(GetWorkspaceAsync_Builds_Attention_Queue_With_Module_Aware_Actions));

        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.Modules.Add(new Module
            {
                Id = 11,
                ModuleCode = "CAPITAL_SUPERVISION",
                ModuleName = "Capital Supervision",
                RegulatorCode = "CBN",
                DefaultFrequency = "Monthly",
                SheetCount = 6,
                CreatedAt = DateTime.UtcNow
            });

            db.ReturnPeriods.Add(new ReturnPeriod
            {
                Id = 243,
                TenantId = tenantId,
                ModuleId = 11,
                Year = 2026,
                Month = 3,
                Frequency = "Monthly",
                ReportingDate = new DateTime(2026, 3, 31),
                DeadlineDate = new DateTime(2026, 4, 15),
                IsOpen = true,
                CreatedAt = DateTime.UtcNow,
                Status = "Open"
            });

            await db.SaveChangesAsync();
        }

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

        var institutionRepository = new Mock<IInstitutionRepository>();
        institutionRepository
            .Setup(x => x.GetByTenant(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Institution
                {
                    Id = 44,
                    TenantId = tenantId,
                    InstitutionName = "Capital Trust Bank",
                    IsActive = true
                }
            ]);

        var submissionRepository = new Mock<ISubmissionRepository>();
        submissionRepository
            .Setup(x => x.GetByInstitution(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateSubmission(7001, tenantId, 44, "CAP_BUF", SubmissionStatus.Rejected, new DateTime(2026, 3, 12, 9, 0, 0, DateTimeKind.Utc), 243, errorCount: 2),
                CreateSubmission(7002, tenantId, 44, "CAP_PLN", SubmissionStatus.PendingApproval, new DateTime(2026, 3, 12, 10, 0, 0, DateTimeKind.Utc), 243),
                CreateSubmission(7003, tenantId, 44, "CAP_STK", SubmissionStatus.AcceptedWithWarnings, new DateTime(2026, 3, 12, 11, 0, 0, DateTimeKind.Utc), 243, warningCount: 3)
            ]);

        var templateCache = new Mock<ITemplateMetadataCache>();
        templateCache
            .Setup(x => x.GetAllPublishedTemplates(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateTemplate("CAP_BUF"),
                CreateTemplate("CAP_PLN"),
                CreateTemplate("CAP_STK")
            ]);

        var dashboardService = new Mock<IDashboardService>();
        dashboardService
            .Setup(x => x.GetModuleDashboard(tenantId, "CAPITAL_SUPERVISION", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleDashboardData
            {
                ModuleCode = "CAPITAL_SUPERVISION",
                ModuleName = "Capital Supervision",
                Periods =
                [
                    new ModulePeriodStatusItem
                    {
                        PeriodId = 243,
                        Label = "Mar 2026",
                        Status = "Open",
                        RagClass = "amber",
                        CompletionPercent = 72,
                        ValidationErrorCount = 2,
                        ValidationWarningCount = 3,
                        Deadline = new DateTime(2026, 4, 15)
                    }
                ]
            });

        var sut = new ModuleWorkspaceService(
            new TestTenantContext { CurrentTenantId = tenantId },
            entitlementService.Object,
            institutionRepository.Object,
            submissionRepository.Object,
            templateCache.Object,
            dashboardService.Object,
            dbFactory,
            new KnowledgeBaseService(dbFactory),
            Mock.Of<ILogger<ModuleWorkspaceService>>());

        var result = await sut.GetWorkspaceAsync("capital-supervision");

        result.Should().NotBeNull();
        result!.AttentionItems.Should().HaveCount(3);
        result.AttentionItems[0].Title.Should().Contain("CAP_BUF");
        result.AttentionItems[0].ActionLabel.Should().Be("Fix Return");
        result.AttentionItems[0].ActionHref.Should().Be("/submit?module=CAPITAL_SUPERVISION&returnCode=CAP_BUF&periodId=243");
        result.BulkSubmitHref.Should().Be("/submit/bulk?module=CAPITAL_SUPERVISION");

        result.AttentionItems.Should().ContainSingle(x =>
            x.ReturnCode == "CAP_PLN" &&
            x.ActionLabel == "Open Submission" &&
            x.ActionHref == "/submissions/7002");

        result.AttentionItems.Should().ContainSingle(x =>
            x.ReturnCode == "CAP_STK" &&
            x.ActionLabel == "Review Validation" &&
            x.ActionHref == "/validation/hub/7003");

        result.RecentSubmissions.Should().ContainSingle(x =>
            x.ReturnCode == "CAP_BUF" &&
            x.ActionLabel == "Fix Return" &&
            x.ActionHref == "/submit?module=CAPITAL_SUPERVISION&returnCode=CAP_BUF&periodId=243");
    }

    private static CachedTemplate CreateTemplate(string returnCode) =>
        new()
        {
            ReturnCode = returnCode,
            Name = returnCode,
            ModuleCode = "CAPITAL_SUPERVISION",
            ModuleId = 11,
            Frequency = ReturnFrequency.Monthly,
            StructuralCategory = "FixedRow",
            CurrentVersion = new CachedTemplateVersion()
        };

    private static Submission CreateSubmission(
        int submissionId,
        Guid tenantId,
        int institutionId,
        string returnCode,
        SubmissionStatus status,
        DateTime submittedAt,
        int returnPeriodId,
        int errorCount = 0,
        int warningCount = 0)
    {
        var report = ValidationReport.Create(submissionId, tenantId);
        for (var i = 0; i < errorCount; i++)
        {
            report.AddError(new ValidationError
            {
                RuleId = $"ERR-{i + 1}",
                Field = "field_error",
                Message = "Error",
                Severity = ValidationSeverity.Error,
                Category = ValidationCategory.Business
            });
        }

        for (var i = 0; i < warningCount; i++)
        {
            report.AddError(new ValidationError
            {
                RuleId = $"WARN-{i + 1}",
                Field = "field_warning",
                Message = "Warning",
                Severity = ValidationSeverity.Warning,
                Category = ValidationCategory.Business
            });
        }

        return new Submission
        {
            Id = submissionId,
            TenantId = tenantId,
            InstitutionId = institutionId,
            ReturnCode = returnCode,
            ReturnPeriodId = returnPeriodId,
            Status = status,
            SubmittedAt = submittedAt,
            ReturnPeriod = new ReturnPeriod
            {
                Id = returnPeriodId,
                TenantId = tenantId,
                Year = 2026,
                Month = 3,
                Frequency = "Monthly",
                ReportingDate = new DateTime(2026, 3, 31),
                DeadlineDate = new DateTime(2026, 4, 15),
                IsOpen = true
            },
            ValidationReport = report
        };
    }
}
