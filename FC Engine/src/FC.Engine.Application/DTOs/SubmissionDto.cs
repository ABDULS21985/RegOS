namespace FC.Engine.Application.DTOs;

public class SubmissionResultDto
{
    public int SubmissionId { get; set; }
    public string ReturnCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? ProcessingDurationMs { get; set; }
    public ValidationReportDto? ValidationReport { get; set; }
}

public class ValidationReportDto
{
    public bool IsValid { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
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

public class FormulaDto
{
    public int Id { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string FormulaType { get; set; } = string.Empty;
    public string TargetFieldName { get; set; } = string.Empty;
    public string? TargetLineCode { get; set; }
    public string OperandFields { get; set; } = "[]";
    public string? CustomExpression { get; set; }
    public decimal ToleranceAmount { get; set; }
    public decimal? TolerancePercent { get; set; }
    public string Severity { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
