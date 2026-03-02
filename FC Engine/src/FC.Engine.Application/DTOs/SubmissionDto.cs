namespace FC.Engine.Application.DTOs;

public class SubmissionDto
{
    public int Id { get; set; }
    public string InstitutionCode { get; set; } = string.Empty;
    public string ReturnCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public int? ErrorCount { get; set; }
    public int? WarningCount { get; set; }
}

public class SubmissionResultDto
{
    public int SubmissionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public ValidationReportDto? ValidationReport { get; set; }
}
