namespace FC.Engine.Portal.Services;

/// <summary>
/// View model for template selection in the submission wizard.
/// </summary>
public class TemplateSelectItem
{
    public string ReturnCode { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string Frequency { get; set; } = "";
    public string StructuralCategory { get; set; } = "";
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
