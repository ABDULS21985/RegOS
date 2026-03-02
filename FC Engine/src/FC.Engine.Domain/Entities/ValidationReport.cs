using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class ValidationReport
{
    public int Id { get; private set; }
    public int SubmissionId { get; private set; }
    public bool IsValid => !Errors.Any(e => e.Severity == ValidationSeverity.Error);
    public bool HasWarnings => Errors.Any(e => e.Severity == ValidationSeverity.Warning);
    public bool HasErrors => Errors.Any(e => e.Severity == ValidationSeverity.Error);
    public int ErrorCount => Errors.Count(e => e.Severity == ValidationSeverity.Error);
    public int WarningCount => Errors.Count(e => e.Severity == ValidationSeverity.Warning);
    public DateTime ValidatedAt { get; private set; }

    public List<ValidationError> Errors { get; private set; } = new();

    // Navigation
    public Submission? Submission { get; private set; }

    private ValidationReport() { }

    public static ValidationReport Create(int submissionId)
    {
        return new ValidationReport
        {
            SubmissionId = submissionId,
            ValidatedAt = DateTime.UtcNow
        };
    }

    public void AddError(string ruleId, string field, string message,
        ValidationSeverity severity, ValidationCategory category,
        string? expectedValue = null, string? actualValue = null,
        string? referencedReturnCode = null)
    {
        Errors.Add(new ValidationError
        {
            RuleId = ruleId,
            Field = field,
            Message = message,
            Severity = severity,
            Category = category,
            ExpectedValue = expectedValue,
            ActualValue = actualValue,
            ReferencedReturnCode = referencedReturnCode
        });
    }

    public void AddErrors(IEnumerable<ValidationError> errors)
    {
        Errors.AddRange(errors);
    }

    public void FinalizeAt(DateTime timestamp)
    {
        ValidatedAt = timestamp;
    }
}
