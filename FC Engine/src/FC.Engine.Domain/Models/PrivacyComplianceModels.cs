using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Models;

public class DataSubjectPackage
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DataSubjectProfile Profile { get; set; } = new();
    public List<DataEntryAuditItem> ReturnDataEntries { get; set; } = new();
    public List<WorkflowActionItem> WorkflowActions { get; set; } = new();
    public List<LoginHistoryItem> LoginHistory { get; set; } = new();
    public List<UserNotificationItem> Notifications { get; set; } = new();
    public List<ConsentHistoryItem> ConsentHistory { get; set; } = new();
}

public class DataSubjectProfile
{
    public int UserId { get; set; }
    public string UserType { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DataEntryAuditItem
{
    public int SubmissionId { get; set; }
    public string ReturnCode { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangeSource { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}

public class WorkflowActionItem
{
    public int SubmissionId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ActionAt { get; set; }
}

public class LoginHistoryItem
{
    public DateTime AttemptedAt { get; set; }
    public bool Succeeded { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? FailureReason { get; set; }
}

public class UserNotificationItem
{
    public string EventType { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ConsentHistoryItem
{
    public ConsentType ConsentType { get; set; }
    public string PolicyVersion { get; set; } = string.Empty;
    public bool ConsentGiven { get; set; }
    public string ConsentMethod { get; set; } = string.Empty;
    public DateTime ConsentedAt { get; set; }
    public DateTime? WithdrawnAt { get; set; }
}

public class DataBreachReport
{
    public Guid? TenantId { get; set; }
    public DataBreachSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? DataSubjectsAffected { get; set; }
    public List<string> DataCategoriesAffected { get; set; } = new();
}

public class DpoDashboardData
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<DsarMetricItem> DsarMetrics { get; set; } = new();
    public decimal DsarSlaWithinDeadlinePercent { get; set; }
    public decimal ConsentRateCurrentPolicyPercent { get; set; }
    public int OpenBreachCount { get; set; }
    public int PendingNitdaNotifications { get; set; }
    public List<BreachCountdownItem> BreachCountdowns { get; set; } = new();
    public List<ProcessingActivityCountItem> ActivityCountsByModule { get; set; } = new();
    public int RetentionDueThisMonth { get; set; }
    public List<PrivacyImpactAssessmentItem> PrivacyImpactStatusByModule { get; set; } = new();
}

public class DsarMetricItem
{
    public DataSubjectRequestType RequestType { get; set; }
    public DataSubjectRequestStatus Status { get; set; }
    public int Count { get; set; }
}

public class BreachCountdownItem
{
    public int IncidentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DataBreachSeverity Severity { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime? NitdaNotificationDeadline { get; set; }
    public double HoursRemaining { get; set; }
}

public class ProcessingActivityCountItem
{
    public string ModuleCode { get; set; } = string.Empty;
    public int ActivityCount { get; set; }
}

public class PrivacyImpactAssessmentItem
{
    public string ModuleCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int ActivityCount { get; set; }
}

public class ConsentCaptureRequest
{
    public Guid TenantId { get; set; }
    public int UserId { get; set; }
    public string UserType { get; set; } = "InstitutionUser";
    public ConsentType ConsentType { get; set; }
    public bool ConsentGiven { get; set; }
    public string ConsentMethod { get; set; } = "checkbox";
    public string? PolicyVersion { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
