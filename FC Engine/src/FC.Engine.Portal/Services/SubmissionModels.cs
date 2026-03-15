using FC.Engine.Application.DTOs;

namespace FC.Engine.Portal.Services;

/// <summary>
/// View model for the submission list page — one item per submission.
/// </summary>
public class SubmissionListItem
{
    public int Id { get; set; }
    public string ReturnCode { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string? ModuleCode { get; set; }
    public string PeriodLabel { get; set; } = "—";
    public DateTime SubmittedAt { get; set; }
    public string Status { get; set; } = "";
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int? ProcessingDurationMs { get; set; }
    public DateTime? DeadlineDate { get; set; }
    public string AssigneeInitials { get; set; } = "ME";
    public bool IsCurrentUser { get; set; } = true;
}

/// <summary>
/// View model for the submission detail page.
/// </summary>
public class SubmissionDetailModel
{
    public int Id { get; set; }
    public string ReturnCode { get; set; } = "";
    public int InstitutionId { get; set; }
    public int ReturnPeriodId { get; set; }
    public string Status { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ProcessingDurationMs { get; set; }
    public string? RawXml { get; set; }
    public bool ApprovalRequired { get; set; }
    public int? SubmittedByUserId { get; set; }
    public Guid TenantId { get; set; }

    // Resolved display values
    public string TemplateName { get; set; } = "";
    public string? ModuleCode { get; set; }
    public string PeriodLabel { get; set; } = "—";
    public string? SubmitterName { get; set; }

    // Validation data (unified as DTOs)
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<ValidationErrorDto> ValidationErrors { get; set; } = new();

    // Approval data
    public SubmissionApprovalModel? Approval { get; set; }
}

/// <summary>
/// View model for the approval record on a submission detail.
/// </summary>
public class SubmissionApprovalModel
{
    public int Id { get; set; }
    public int RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? SubmitterNotes { get; set; }
    public string Status { get; set; } = "";
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewerComments { get; set; }
    public string? ReviewerName { get; set; }
    public int? OriginalSubmissionId { get; set; }
}

/// <summary>
/// View model for template selection in the submission wizard.
/// </summary>
public class TemplateSelectItem
{
    public string ReturnCode { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string Frequency { get; set; } = "";
    public string StructuralCategory { get; set; } = "";
    public string? ModuleCode { get; set; }
    public bool AlreadySubmitted { get; set; }
}

/// <summary>
/// View model for period selection in the submission wizard.
/// </summary>
public class PeriodSelectItem
{
    public int ReturnPeriodId { get; set; }
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public DateTime ReportingDate { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public bool HasExistingSubmission { get; set; }
    /// <summary>Effective deadline for this period (override date takes precedence over base deadline).</summary>
    public DateTime DeadlineDate { get; set; }
    /// <summary>Period is past its filing window with no extension granted — no new submissions allowed.</summary>
    public bool IsLocked { get; set; }
    /// <summary>Status of the existing submission for this period, if one exists.</summary>
    public FC.Engine.Domain.Enums.SubmissionStatus? ExistingSubmissionStatus { get; set; }
    /// <summary>ID of the existing submission for this period, used to navigate to read-only view.</summary>
    public int? ExistingSubmissionId { get; set; }
}

/// <summary>
/// Flattened validation error for display in the wizard.
/// </summary>
public class ValidationDisplayError
{
    public string RuleId { get; set; } = "";
    public string Field { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsError { get; set; }
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
}
