using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Portal.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace FC.Engine.Portal.Tests.Services;

public class ExportServiceTests
{
    [Fact]
    public async Task BuildComplianceReportAsync_Annotates_Submissions_With_Module_Metadata()
    {
        var tenantId = Guid.NewGuid();
        var submission = new Submission
        {
            Id = 5101,
            TenantId = tenantId,
            InstitutionId = 44,
            ReturnCode = "MRM_INV",
            ReturnPeriodId = 243,
            Status = SubmissionStatus.Accepted,
            SubmittedAt = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc),
            SubmittedByUserId = 900,
            ReturnPeriod = new ReturnPeriod
            {
                Id = 243,
                TenantId = tenantId,
                Year = 2026,
                Month = 3,
                Frequency = "Monthly"
            }
        };
        var complianceValidationReport = ValidationReport.Create(submission.Id, tenantId);
        complianceValidationReport.AddError(new ValidationError
        {
            RuleId = "WARN-1",
            Field = "gini_score",
            Message = "Review metric drift",
            Severity = ValidationSeverity.Warning,
            Category = ValidationCategory.Business
        });
        submission.ValidationReport = complianceValidationReport;

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(x => x.GetByInstitution(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync([submission]);

        var institutionRepo = new Mock<IInstitutionRepository>();
        institutionRepo
            .Setup(x => x.GetById(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Institution
            {
                Id = 44,
                TenantId = tenantId,
                InstitutionName = "Model Bank",
                InstitutionCode = "MB001"
            });

        var userRepo = new Mock<IInstitutionUserRepository>();
        userRepo
            .Setup(x => x.GetByInstitution(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new InstitutionUser
                {
                    Id = 900,
                    DisplayName = "Kemi"
                }
            ]);

        var templateCache = new Mock<ITemplateMetadataCache>();
        templateCache
            .Setup(x => x.GetPublishedTemplate("MRM_INV", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedTemplate
            {
                ReturnCode = "MRM_INV",
                Name = "Model Inventory",
                ModuleCode = "MODEL_RISK",
                CurrentVersion = new CachedTemplateVersion()
            });

        var brandingService = new Mock<ITenantBrandingService>();
        brandingService
            .Setup(x => x.GetBrandingConfig(tenantId))
            .ReturnsAsync(BrandingConfig.WithDefaults());

        var sut = new ExportService(
            submissionRepo.Object,
            institutionRepo.Object,
            userRepo.Object,
            Mock.Of<ISubmissionApprovalRepository>(),
            brandingService.Object,
            templateCache.Object);

        var report = await sut.BuildComplianceReportAsync(44, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        report.Submissions.Should().ContainSingle();
        report.Submissions[0].ModuleCode.Should().Be("MODEL_RISK");
        report.Submissions[0].ModuleName.Should().Be(PortalSubmissionLinkBuilder.ResolveModuleName("MODEL_RISK"));
        report.Submissions[0].WorkspaceHref.Should().Be("/workflows/model-risk");
    }

    [Fact]
    public async Task BuildAuditTrailAsync_Annotates_Entries_With_Module_Metadata()
    {
        var tenantId = Guid.NewGuid();
        var submission = new Submission
        {
            Id = 6201,
            TenantId = tenantId,
            InstitutionId = 44,
            ReturnCode = "OPS_IBS",
            ReturnPeriodId = 243,
            Status = SubmissionStatus.PendingApproval,
            SubmittedAt = new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc),
            SubmittedByUserId = 700,
            ReturnPeriod = new ReturnPeriod
            {
                Id = 243,
                TenantId = tenantId,
                Year = 2026,
                Month = 3,
                Frequency = "Monthly"
            }
        };

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(x => x.GetByInstitution(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync([submission]);

        var institutionRepo = new Mock<IInstitutionRepository>();
        institutionRepo
            .Setup(x => x.GetById(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Institution
            {
                Id = 44,
                TenantId = tenantId,
                InstitutionName = "Resilience Bank",
                InstitutionCode = "RB001"
            });

        var userRepo = new Mock<IInstitutionUserRepository>();
        userRepo
            .Setup(x => x.GetByInstitution(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new InstitutionUser
                {
                    Id = 700,
                    DisplayName = "Ngozi"
                }
            ]);

        var approvalRepo = new Mock<ISubmissionApprovalRepository>();
        approvalRepo
            .Setup(x => x.GetBySubmission(submission.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubmissionApproval?)null);
        approvalRepo
            .Setup(x => x.GetBySubmissionIds(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SubmissionApproval>());

        var templateCache = new Mock<ITemplateMetadataCache>();
        templateCache
            .Setup(x => x.GetPublishedTemplate("OPS_IBS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedTemplate
            {
                ReturnCode = "OPS_IBS",
                Name = "Important Business Services",
                ModuleCode = "OPS_RESILIENCE",
                CurrentVersion = new CachedTemplateVersion()
            });

        var brandingService = new Mock<ITenantBrandingService>();
        brandingService
            .Setup(x => x.GetBrandingConfig(tenantId))
            .ReturnsAsync(BrandingConfig.WithDefaults());

        var sut = new ExportService(
            submissionRepo.Object,
            institutionRepo.Object,
            userRepo.Object,
            approvalRepo.Object,
            brandingService.Object,
            templateCache.Object);

        var trail = await sut.BuildAuditTrailAsync(44, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        trail.Entries.Should().ContainSingle();
        trail.Entries[0].ModuleCode.Should().Be("OPS_RESILIENCE");
        trail.Entries[0].ModuleName.Should().Be(PortalSubmissionLinkBuilder.ResolveModuleName("OPS_RESILIENCE"));
        trail.Entries[0].WorkspaceHref.Should().Be("/workflows/ops-resilience");
    }
}
