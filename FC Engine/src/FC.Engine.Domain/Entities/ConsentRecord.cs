using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class ConsentRecord
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int UserId { get; set; }
    public string UserType { get; set; } = "InstitutionUser";
    public ConsentType ConsentType { get; set; }
    public string PolicyVersion { get; set; } = "1.0";
    public bool ConsentGiven { get; set; }
    public string ConsentMethod { get; set; } = "checkbox";
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime ConsentedAt { get; set; } = DateTime.UtcNow;
    public DateTime? WithdrawnAt { get; set; }
}
