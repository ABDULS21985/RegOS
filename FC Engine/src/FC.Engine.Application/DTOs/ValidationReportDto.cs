namespace FC.Engine.Application.DTOs;

public class ValidationReportDto
{
    public int SubmissionId { get; set; }
    public bool IsValid { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public DateTime ValidatedAt { get; set; }
    public List<ValidationErrorDto> Errors { get; set; } = new();
}

public class ValidationErrorDto
{
    public string RuleId { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
    public string? ReferencedReturnCode { get; set; }
}
