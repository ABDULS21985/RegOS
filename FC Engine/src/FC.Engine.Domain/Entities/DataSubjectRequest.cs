using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class DataSubjectRequest
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public DataSubjectRequestType RequestType { get; set; }
    public int RequestedBy { get; set; }
    public string RequestedByUserType { get; set; } = "InstitutionUser";
    public DataSubjectRequestStatus Status { get; set; } = DataSubjectRequestStatus.Received;
    public string? Description { get; set; }
    public string? ResponseNotes { get; set; }
    public int? ProcessedBy { get; set; }
    public string? DataPackagePath { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
