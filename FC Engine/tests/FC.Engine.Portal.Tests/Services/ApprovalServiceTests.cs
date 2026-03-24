using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Domain.Notifications;
using FC.Engine.Portal.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FC.Engine.Portal.Tests.Services;

public class ApprovalServiceTests
{
    [Fact]
    public async Task GetPendingApprovals_Enriches_Module_Metadata_For_Portal_Workflows()
    {
        var tenantId = Guid.NewGuid();
        var submission = new Submission
        {
            Id = 4401,
            TenantId = tenantId,
            InstitutionId = 44,
            ReturnCode = "CAP_BUF",
            ReturnPeriodId = 243,
            Status = SubmissionStatus.PendingApproval,
            SubmittedAt = new DateTime(2026, 3, 12, 10, 0, 0, DateTimeKind.Utc),
            ReturnPeriod = new ReturnPeriod
            {
                Id = 243,
                TenantId = tenantId,
                Year = 2026,
                Month = 3,
                Frequency = "Monthly"
            }
        };

        var approvalRepo = new Mock<ISubmissionApprovalRepository>();
        approvalRepo
            .Setup(x => x.GetPendingByInstitution(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new SubmissionApproval
                {
                    Id = 91,
                    SubmissionId = submission.Id,
                    RequestedAt = submission.SubmittedAt!.Value,
                    SubmitterNotes = "Ready for checker review",
                    Submission = submission,
                    RequestedBy = new InstitutionUser
                    {
                        Id = 7001,
                        DisplayName = "Amina Yusuf"
                    }
                }
            ]);

        var submissionRepo = new Mock<ISubmissionRepository>();
        var validationReport = ValidationReport.Create(submission.Id, tenantId);
        validationReport.AddError(new ValidationError
        {
            RuleId = "WARN-1",
            Field = "capital_buffer",
            Message = "Warning only",
            Severity = ValidationSeverity.Warning,
            Category = ValidationCategory.Business
        });
        validationReport.AddError(new ValidationError
        {
            RuleId = "WARN-2",
            Field = "capital_ratio",
            Message = "Warning only",
            Severity = ValidationSeverity.Warning,
            Category = ValidationCategory.Business
        });

        var submissionWithReport = new Submission
        {
            Id = submission.Id,
            TenantId = tenantId,
            InstitutionId = 44,
            ReturnCode = submission.ReturnCode,
            ReturnPeriodId = submission.ReturnPeriodId,
            Status = SubmissionStatus.PendingApproval,
            ValidationReport = validationReport
        };

        submissionRepo
            .Setup(x => x.GetByIdWithReport(submission.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submissionWithReport);

        submissionRepo
            .Setup(x => x.GetByIdsWithReport(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Submission> { submissionWithReport });

        var templateCache = new Mock<ITemplateMetadataCache>();
        templateCache
            .Setup(x => x.GetPublishedTemplate("CAP_BUF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedTemplate
            {
                ReturnCode = "CAP_BUF",
                Name = "Capital Buffer Register",
                ModuleCode = "CAPITAL_SUPERVISION",
                CurrentVersion = new CachedTemplateVersion()
            });

        var workflowService = new WorkflowService(
            submissionRepo.Object,
            approvalRepo.Object,
            Mock.Of<IInstitutionUserRepository>(),
            null,
            Mock.Of<INotificationOrchestrator>(),
            Mock.Of<IFilingCalendarService>(),
            NullLogger<WorkflowService>.Instance);

        var sut = new ApprovalService(
            submissionRepo.Object,
            approvalRepo.Object,
            Mock.Of<IInstitutionUserRepository>(),
            templateCache.Object,
            workflowService);

        var result = await sut.GetPendingApprovals(44);

        result.Should().ContainSingle();
        result[0].ModuleCode.Should().Be("CAPITAL_SUPERVISION");
        result[0].ModuleName.Should().Be("Capital Supervision");
        result[0].WorkspaceHref.Should().Be("/workflows/capital-supervision");
        result[0].WarningCount.Should().Be(2);
        result[0].ValidationPassed.Should().BeTrue();
        result[0].RequestedByUserId.Should().Be(7001);
    }

    [Fact]
    public async Task GetPendingApprovals_Returns_RequestedByUserId_For_Self_Approval_Prevention()
    {
        var tenantId = Guid.NewGuid();
        var makerUserId = 501;
        var submission = new Submission
        {
            Id = 9901,
            TenantId = tenantId,
            InstitutionId = 10,
            ReturnCode = "FIN_001",
            ReturnPeriodId = 100,
            Status = SubmissionStatus.PendingApproval,
            ReturnPeriod = new ReturnPeriod { Id = 100, TenantId = tenantId, Year = 2026, Month = 3, Frequency = "Monthly" }
        };

        var approvalRepo = new Mock<ISubmissionApprovalRepository>();
        approvalRepo
            .Setup(x => x.GetPendingByInstitution(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new SubmissionApproval
                {
                    Id = 200,
                    SubmissionId = submission.Id,
                    RequestedByUserId = makerUserId,
                    RequestedAt = DateTime.UtcNow,
                    Submission = submission,
                    RequestedBy = new InstitutionUser { Id = makerUserId, DisplayName = "Maker User" }
                }
            ]);

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(x => x.GetByIdsWithReport(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Submission> { submission });

        var templateCache = new Mock<ITemplateMetadataCache>();
        templateCache
            .Setup(x => x.GetPublishedTemplate("FIN_001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedTemplate { ReturnCode = "FIN_001", Name = "Financial Return", CurrentVersion = new CachedTemplateVersion() });

        var workflowService = new WorkflowService(
            submissionRepo.Object, approvalRepo.Object,
            Mock.Of<IInstitutionUserRepository>(), null,
            Mock.Of<INotificationOrchestrator>(),
            Mock.Of<IFilingCalendarService>(),
            NullLogger<WorkflowService>.Instance);

        var sut = new ApprovalService(
            submissionRepo.Object, approvalRepo.Object,
            Mock.Of<IInstitutionUserRepository>(),
            templateCache.Object, workflowService);

        var result = await sut.GetPendingApprovals(10);

        result.Should().ContainSingle();
        result[0].RequestedByUserId.Should().Be(makerUserId,
            "the UI needs the maker's user ID to prevent self-approval");
        result[0].SubmittedBy.Should().Be("Maker User");
    }

    [Fact]
    public async Task Reject_Returns_CommentsRequired_When_Reason_Is_Empty()
    {
        var submissionRepo = new Mock<ISubmissionRepository>();
        var approvalRepo = new Mock<ISubmissionApprovalRepository>();

        var workflowService = new WorkflowService(
            submissionRepo.Object, approvalRepo.Object,
            Mock.Of<IInstitutionUserRepository>(), null,
            Mock.Of<INotificationOrchestrator>(),
            Mock.Of<IFilingCalendarService>(),
            NullLogger<WorkflowService>.Instance);

        var sut = new ApprovalService(
            submissionRepo.Object, approvalRepo.Object,
            Mock.Of<IInstitutionUserRepository>(),
            Mock.Of<ITemplateMetadataCache>(), workflowService);

        var result = await sut.Reject(submissionId: 1, reviewerUserId: 2, comments: "   ");

        result.Should().Be(ApprovalActionResult.CommentsRequired);
    }

    [Fact]
    public async Task Approve_Returns_SelfApprovalNotAllowed_When_Checker_Is_Maker()
    {
        var makerUserId = 42;

        var approvalRepo = new Mock<ISubmissionApprovalRepository>();
        approvalRepo
            .Setup(x => x.GetBySubmission(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmissionApproval
            {
                Id = 1,
                SubmissionId = 100,
                RequestedByUserId = makerUserId,
                Status = ApprovalStatus.Pending
            });

        var submissionRepo = new Mock<ISubmissionRepository>();

        var workflowService = new WorkflowService(
            submissionRepo.Object, approvalRepo.Object,
            Mock.Of<IInstitutionUserRepository>(), null,
            Mock.Of<INotificationOrchestrator>(),
            Mock.Of<IFilingCalendarService>(),
            NullLogger<WorkflowService>.Instance);

        var sut = new ApprovalService(
            submissionRepo.Object, approvalRepo.Object,
            Mock.Of<IInstitutionUserRepository>(),
            Mock.Of<ITemplateMetadataCache>(), workflowService);

        var result = await sut.Approve(submissionId: 100, reviewerUserId: makerUserId);

        result.Should().Be(ApprovalActionResult.SelfApprovalNotAllowed);
    }

    [Fact]
    public async Task Approve_Returns_AlreadyProcessed_When_Not_Pending()
    {
        var approvalRepo = new Mock<ISubmissionApprovalRepository>();
        approvalRepo
            .Setup(x => x.GetBySubmission(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmissionApproval
            {
                Id = 1,
                SubmissionId = 100,
                RequestedByUserId = 10,
                Status = ApprovalStatus.Approved
            });

        var submissionRepo = new Mock<ISubmissionRepository>();

        var workflowService = new WorkflowService(
            submissionRepo.Object, approvalRepo.Object,
            Mock.Of<IInstitutionUserRepository>(), null,
            Mock.Of<INotificationOrchestrator>(),
            Mock.Of<IFilingCalendarService>(),
            NullLogger<WorkflowService>.Instance);

        var sut = new ApprovalService(
            submissionRepo.Object, approvalRepo.Object,
            Mock.Of<IInstitutionUserRepository>(),
            Mock.Of<ITemplateMetadataCache>(), workflowService);

        var result = await sut.Approve(submissionId: 100, reviewerUserId: 20);

        result.Should().Be(ApprovalActionResult.AlreadyProcessed);
    }
}
