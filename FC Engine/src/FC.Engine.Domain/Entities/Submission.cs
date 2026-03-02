using FC.Engine.Domain.Enums;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Domain.Entities;

public class Submission
{
    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public int ReturnPeriodId { get; private set; }
    public string ReturnCodeValue { get; private set; } = string.Empty;
    public SubmissionStatus Status { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigation
    public Institution? Institution { get; private set; }
    public ReturnPeriod? ReturnPeriod { get; private set; }
    public ValidationReport? ValidationReport { get; private set; }

    private Submission() { }

    public static Submission Create(int institutionId, int returnPeriodId, string returnCode)
    {
        return new Submission
        {
            InstitutionId = institutionId,
            ReturnPeriodId = returnPeriodId,
            ReturnCodeValue = returnCode,
            Status = SubmissionStatus.Draft,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public ReturnCode GetReturnCode() => ReturnCode.Parse(ReturnCodeValue);

    public void MarkParsing()
    {
        Status = SubmissionStatus.Parsing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkValidating()
    {
        Status = SubmissionStatus.Validating;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAccepted()
    {
        Status = SubmissionStatus.Accepted;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAcceptedWithWarnings()
    {
        Status = SubmissionStatus.AcceptedWithWarnings;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkRejected()
    {
        Status = SubmissionStatus.Rejected;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AttachValidationReport(ValidationReport report)
    {
        ValidationReport = report;
        UpdatedAt = DateTime.UtcNow;
    }
}
