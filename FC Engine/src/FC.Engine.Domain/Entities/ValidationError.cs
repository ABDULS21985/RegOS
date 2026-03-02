using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class ValidationError
{
    public int Id { get; set; }
    public int ValidationReportId { get; set; }
    public string RuleId { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ValidationSeverity Severity { get; set; }
    public ValidationCategory Category { get; set; }
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
    public string? ReferencedReturnCode { get; set; }
}
