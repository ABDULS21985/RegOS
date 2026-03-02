namespace FC.Engine.Domain.Entities;

public class Institution
{
    public int Id { get; set; }
    public string InstitutionCode { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
    public string? LicenseType { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
