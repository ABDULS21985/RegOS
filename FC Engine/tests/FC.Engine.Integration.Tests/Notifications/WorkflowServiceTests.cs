using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using SubmissionEntity = FC.Engine.Domain.Entities.Submission;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Notifications;
using FC.Engine.Portal.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FC.Engine.Integration.Tests.Notifications;

public class WorkflowServiceTests
{
    [Fact]
    public async Task Return_Approved_Notifies_Maker()
    {
        var tenantId = Guid.NewGuid();
        var submissionId = 301;
        var makerUserId = 41;
        var checkerUserId = 77;

        var submissionRepo = new Mock<ISubmissionRepository>();
        var approvalRepo = new Mock<ISubmissionApprovalRepository>();
        var userRepo = new Mock<IInstitutionUserRepository>();
        var notificationOrchestrator = new Mock<INotificationOrchestrator>();
        var filingCalendarService = new Mock<IFilingCalendarService>();

        var approval = new SubmissionApproval
        {
            SubmissionId = submissionId,
            RequestedByUserId = makerUserId,
            Status = ApprovalStatus.Pending
        };

        var submission = new SubmissionEntity
        {
            Id = submissionId,
            TenantId = tenantId,
            InstitutionId = 12,
            ReturnPeriodId = 55,
            ReturnCode = "MFCR 310",
            ReturnPeriod = new ReturnPeriod { Year = 2026, Month = 3 },
            Status = SubmissionStatus.PendingApproval
        };

        approvalRepo.Setup(x => x.GetBySubmission(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);
        approvalRepo.Setup(x => x.Update(approval, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        submissionRepo.Setup(x => x.GetByIdWithReport(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);
        submissionRepo.Setup(x => x.Update(submission, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        userRepo.Setup(x => x.GetById(checkerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstitutionUser
            {
                Id = checkerUserId,
                DisplayName = "Checker Jane",
                Role = InstitutionRole.Checker,
                PasswordHash = "hash",
                TenantId = tenantId,
                InstitutionId = 12
            });

        notificationOrchestrator
            .Setup(x => x.Notify(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        filingCalendarService
            .Setup(x => x.RecordSla(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new WorkflowService(
            submissionRepo.Object,
            approvalRepo.Object,
            userRepo.Object,
            null, // anomalyDetectionService
            notificationOrchestrator.Object,
            filingCalendarService.Object,
            NullLogger<WorkflowService>.Instance);

        var result = await sut.Approve(submissionId, checkerUserId, "Looks good", CancellationToken.None);

        result.Should().Be(ApprovalActionResult.Success);
        notificationOrchestrator.Verify(x => x.Notify(
            It.Is<NotificationRequest>(n =>
                n.TenantId == tenantId &&
                n.EventType == NotificationEvents.ReturnApproved &&
                n.RecipientUserIds.SequenceEqual(new[] { makerUserId }) &&
                n.Priority == NotificationPriority.Normal &&
                n.ActionUrl == $"/submissions/{submissionId}"),
            It.IsAny<CancellationToken>()), Times.Once);

        filingCalendarService.Verify(
            x => x.RecordSla(submission.ReturnPeriodId, submissionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Return_Rejected_Notifies_Maker_With_Reason()
    {
        var tenantId = Guid.NewGuid();
        var submissionId = 302;
        var makerUserId = 41;
        var checkerUserId = 77;
        var rejectionReason = "Figures in Section 2 do not reconcile with the GL extract";

        var submissionRepo = new Mock<ISubmissionRepository>();
        var approvalRepo = new Mock<ISubmissionApprovalRepository>();
        var userRepo = new Mock<IInstitutionUserRepository>();
        var notificationOrchestrator = new Mock<INotificationOrchestrator>();
        var filingCalendarService = new Mock<IFilingCalendarService>();

        var approval = new SubmissionApproval
        {
            SubmissionId = submissionId,
            RequestedByUserId = makerUserId,
            Status = ApprovalStatus.Pending
        };

        var submission = new SubmissionEntity
        {
            Id = submissionId,
            TenantId = tenantId,
            InstitutionId = 12,
            ReturnPeriodId = 55,
            ReturnCode = "MFCR 310",
            ReturnPeriod = new ReturnPeriod { Year = 2026, Month = 3 },
            Status = SubmissionStatus.PendingApproval
        };

        approvalRepo.Setup(x => x.GetBySubmission(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);
        approvalRepo.Setup(x => x.Update(approval, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        submissionRepo.Setup(x => x.GetById(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);
        submissionRepo.Setup(x => x.Update(submission, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        userRepo.Setup(x => x.GetById(checkerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstitutionUser
            {
                Id = checkerUserId,
                DisplayName = "Checker Jane",
                Role = InstitutionRole.Checker,
                PasswordHash = "hash",
                TenantId = tenantId,
                InstitutionId = 12
            });

        notificationOrchestrator
            .Setup(x => x.Notify(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new WorkflowService(
            submissionRepo.Object,
            approvalRepo.Object,
            userRepo.Object,
            null,
            notificationOrchestrator.Object,
            filingCalendarService.Object,
            NullLogger<WorkflowService>.Instance);

        var result = await sut.Reject(submissionId, checkerUserId, rejectionReason, CancellationToken.None);

        result.Should().Be(ApprovalActionResult.Success);

        // Verify approval record was updated
        approval.Status.Should().Be(ApprovalStatus.Rejected);
        approval.ReviewedByUserId.Should().Be(checkerUserId);
        approval.ReviewerComments.Should().Be(rejectionReason);
        approval.ReviewedAt.Should().NotBeNull();

        // Verify submission status changed to ApprovalRejected
        submission.Status.Should().Be(SubmissionStatus.ApprovalRejected);

        // Verify notification sent to maker with high priority
        notificationOrchestrator.Verify(x => x.Notify(
            It.Is<NotificationRequest>(n =>
                n.TenantId == tenantId &&
                n.EventType == NotificationEvents.ReturnRejected &&
                n.RecipientUserIds.SequenceEqual(new[] { makerUserId }) &&
                n.Priority == NotificationPriority.High &&
                n.Message.Contains(rejectionReason)),
            It.IsAny<CancellationToken>()), Times.Once);

        // SLA should NOT be recorded on rejection
        filingCalendarService.Verify(
            x => x.RecordSla(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Reject_Blocks_Self_Rejection()
    {
        var makerUserId = 41;

        var approvalRepo = new Mock<ISubmissionApprovalRepository>();
        approvalRepo.Setup(x => x.GetBySubmission(500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmissionApproval
            {
                SubmissionId = 500,
                RequestedByUserId = makerUserId,
                Status = ApprovalStatus.Pending
            });

        var sut = new WorkflowService(
            Mock.Of<ISubmissionRepository>(),
            approvalRepo.Object,
            Mock.Of<IInstitutionUserRepository>(),
            null,
            Mock.Of<INotificationOrchestrator>(),
            Mock.Of<IFilingCalendarService>(),
            NullLogger<WorkflowService>.Instance);

        var result = await sut.Reject(500, makerUserId, "Self-reject attempt");

        result.Should().Be(ApprovalActionResult.SelfApprovalNotAllowed);
    }
}
