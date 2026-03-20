using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;

namespace FC.Engine.Portal.Services;

/// <summary>
/// Manages maker-checker approval workflows for the FI Portal.
/// Handles pending approval queries, approve/reject actions, and re-submission linking.
/// </summary>
public class ApprovalService
{
    private readonly ISubmissionRepository _submissionRepo;
    private readonly ISubmissionApprovalRepository _approvalRepo;
    private readonly IInstitutionUserRepository _userRepo;
    private readonly ITemplateMetadataCache _cache;
    private readonly WorkflowService _workflowService;

    public ApprovalService(
        ISubmissionRepository submissionRepo,
        ISubmissionApprovalRepository approvalRepo,
        IInstitutionUserRepository userRepo,
        ITemplateMetadataCache cache,
        WorkflowService workflowService)
    {
        _submissionRepo = submissionRepo;
        _approvalRepo = approvalRepo;
        _userRepo = userRepo;
        _cache = cache;
        _workflowService = workflowService;
    }

    // ── Query Methods ────────────────────────────────────────────

    /// <summary>
    /// Get all pending approval items for the institution, enriched with display data.
    /// </summary>
    public async Task<List<PendingApprovalItem>> GetPendingApprovals(
        int institutionId, CancellationToken ct = default)
    {
        var approvals = await _approvalRepo.GetPendingByInstitution(institutionId, ct);
        var items = new List<PendingApprovalItem>();

        foreach (var approval in approvals)
        {
            var submission = approval.Submission;
            if (submission is null) continue;

            var submitterName = approval.RequestedBy?.DisplayName ?? "Unknown";

            string templateName = "";
            string? moduleCode = null;
            try
            {
                var template = await _cache.GetPublishedTemplate(submission.ReturnCode, ct);
                templateName = template.Name;
                moduleCode = template.ModuleCode;
            }
            catch
            {
                templateName = submission.ReturnCode;
            }

            string period = "";
            if (submission.ReturnPeriod is not null)
            {
                period = new DateTime(submission.ReturnPeriod.Year, submission.ReturnPeriod.Month, 1)
                    .ToString("MMM yyyy");
            }

            // Load validation data
            var fullSub = await _submissionRepo.GetByIdWithReport(submission.Id, ct);
            var errCount = fullSub?.ValidationReport?.ErrorCount ?? 0;
            var warnCount = fullSub?.ValidationReport?.WarningCount ?? 0;

            items.Add(new PendingApprovalItem
            {
                SubmissionId = submission.Id,
                ReturnCode = submission.ReturnCode,
                TemplateName = templateName,
                ModuleCode = moduleCode,
                ModuleName = PortalSubmissionLinkBuilder.ResolveModuleName(moduleCode),
                WorkspaceHref = PortalSubmissionLinkBuilder.ResolveWorkspaceHref(moduleCode),
                Period = period,
                SubmittedBy = submitterName,
                SubmittedAt = approval.RequestedAt,
                SubmitterNotes = approval.SubmitterNotes,
                ErrorCount = errCount,
                WarningCount = warnCount,
                ValidationPassed = errCount == 0
            });
        }

        return items.OrderByDescending(i => i.SubmittedAt).ToList();
    }

    /// <summary>
    /// Get the count of pending approvals for the institution (for badge display).
    /// </summary>
    public async Task<int> GetPendingCount(int institutionId, CancellationToken ct = default)
    {
        var approvals = await _approvalRepo.GetPendingByInstitution(institutionId, ct);
        return approvals.Count;
    }

    /// <summary>
    /// Get detailed approval info for a specific submission.
    /// </summary>
    public async Task<ApprovalDetailModel?> GetApprovalDetail(
        int submissionId, CancellationToken ct = default)
    {
        var approval = await _approvalRepo.GetBySubmission(submissionId, ct);
        if (approval is null) return null;

        var submitterName = approval.RequestedBy?.DisplayName ?? "Unknown";
        string? reviewerName = null;

        if (approval.ReviewedBy is not null)
        {
            reviewerName = approval.ReviewedBy.DisplayName;
        }
        else if (approval.ReviewedByUserId.HasValue)
        {
            var reviewer = await _userRepo.GetById(approval.ReviewedByUserId.Value, ct);
            reviewerName = reviewer?.DisplayName ?? "Unknown";
        }

        // Build timeline
        var history = new List<ApprovalHistoryEvent>();

        // Check for original submission (re-submission chain)
        if (approval.OriginalSubmissionId.HasValue)
        {
            var originalApproval = await _approvalRepo.GetBySubmission(
                approval.OriginalSubmissionId.Value, ct);
            if (originalApproval is not null)
            {
                var origSubmitterName = originalApproval.RequestedBy?.DisplayName ?? submitterName;

                history.Add(new ApprovalHistoryEvent
                {
                    EventType = "Submitted",
                    UserName = origSubmitterName,
                    Timestamp = originalApproval.RequestedAt,
                    Notes = originalApproval.SubmitterNotes,
                    SubmissionId = approval.OriginalSubmissionId.Value
                });

                if (originalApproval.Status == ApprovalStatus.Rejected)
                {
                    string? origReviewerName = null;
                    if (originalApproval.ReviewedByUserId.HasValue)
                    {
                        var origReviewer = await _userRepo.GetById(
                            originalApproval.ReviewedByUserId.Value, ct);
                        origReviewerName = origReviewer?.DisplayName ?? "Unknown";
                    }

                    history.Add(new ApprovalHistoryEvent
                    {
                        EventType = "Rejected",
                        UserName = origReviewerName ?? "Unknown",
                        Timestamp = originalApproval.ReviewedAt ?? originalApproval.RequestedAt,
                        Notes = originalApproval.ReviewerComments,
                        SubmissionId = approval.OriginalSubmissionId.Value
                    });
                }

                history.Add(new ApprovalHistoryEvent
                {
                    EventType = "Re-submitted",
                    UserName = submitterName,
                    Timestamp = approval.RequestedAt,
                    Notes = approval.SubmitterNotes,
                    SubmissionId = submissionId
                });
            }
        }
        else
        {
            history.Add(new ApprovalHistoryEvent
            {
                EventType = "Submitted",
                UserName = submitterName,
                Timestamp = approval.RequestedAt,
                Notes = approval.SubmitterNotes,
                SubmissionId = submissionId
            });
        }

        // Add review event if reviewed
        if (approval.Status != ApprovalStatus.Pending && approval.ReviewedAt.HasValue)
        {
            history.Add(new ApprovalHistoryEvent
            {
                EventType = approval.Status == ApprovalStatus.Approved ? "Approved" : "Rejected",
                UserName = reviewerName ?? "Unknown",
                Timestamp = approval.ReviewedAt.Value,
                Notes = approval.ReviewerComments,
                SubmissionId = submissionId
            });
        }

        return new ApprovalDetailModel
        {
            ApprovalId = approval.Id,
            SubmissionId = submissionId,
            Status = approval.Status,
            SubmitterName = submitterName,
            SubmittedAt = approval.RequestedAt,
            SubmitterNotes = approval.SubmitterNotes,
            ReviewerName = reviewerName,
            ReviewedAt = approval.ReviewedAt,
            ReviewerComments = approval.ReviewerComments,
            OriginalSubmissionId = approval.OriginalSubmissionId,
            History = history.OrderBy(h => h.Timestamp).ToList()
        };
    }

    // ── Action Methods ───────────────────────────────────────────

    /// <summary>
    /// Approve a pending submission (Checker action).
    /// </summary>
    public async Task<ApprovalActionResult> Approve(
        int submissionId, int reviewerUserId, string? comments = null,
        CancellationToken ct = default)
    {
        return await _workflowService.Approve(submissionId, reviewerUserId, comments, ct);
    }

    /// <summary>
    /// Reject a pending submission (Checker action).
    /// </summary>
    public async Task<ApprovalActionResult> Reject(
        int submissionId, int reviewerUserId, string comments,
        CancellationToken ct = default)
    {
        return await _workflowService.Reject(submissionId, reviewerUserId, comments, ct);
    }
}

// ── Data Models ──────────────────────────────────────────────────────

public class PendingApprovalItem
{
    public int SubmissionId { get; set; }
    public string ReturnCode { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string? ModuleCode { get; set; }
    public string? ModuleName { get; set; }
    public string? WorkspaceHref { get; set; }
    public string Period { get; set; } = "";
    public string SubmittedBy { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public string? SubmitterNotes { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public bool ValidationPassed { get; set; }
}

public class ApprovalDetailModel
{
    public int ApprovalId { get; set; }
    public int SubmissionId { get; set; }
    public ApprovalStatus Status { get; set; }
    public string SubmitterName { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public string? SubmitterNotes { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewerComments { get; set; }
    public int? OriginalSubmissionId { get; set; }
    public List<ApprovalHistoryEvent> History { get; set; } = new();
}

public class ApprovalHistoryEvent
{
    public string EventType { get; set; } = "";
    public string UserName { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string? Notes { get; set; }
    public int SubmissionId { get; set; }
}

public enum ApprovalActionResult
{
    Success,
    NotFound,
    AlreadyProcessed,
    SelfApprovalNotAllowed,
    CommentsRequired,
    Error
}
