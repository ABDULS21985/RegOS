using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class ExaminerQuery
{
    public int Id { get; set; }

    /// <summary>Institution tenant ID for RLS visibility.</summary>
    public Guid TenantId { get; set; }
    public Guid RegulatorTenantId { get; set; }

    public int SubmissionId { get; set; }
    public string? FieldCode { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public int RaisedBy { get; set; }
    public DateTime RaisedAt { get; set; } = DateTime.UtcNow;
    public string? ResponseText { get; set; }
    public int? RespondedBy { get; set; }
    public DateTime? RespondedAt { get; set; }
    public ExaminerQueryStatus Status { get; set; } = ExaminerQueryStatus.Open;
    public ExaminerQueryPriority Priority { get; set; } = ExaminerQueryPriority.Normal;

    public Submission? Submission { get; set; }
}
