namespace FC.Engine.Domain.Entities;

public class Institution
{
    public int Id { get; private set; }
    public string InstitutionCode { get; private set; } = string.Empty;
    public string InstitutionName { get; private set; } = string.Empty;
    public string? LicenseType { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    private Institution() { }

    public static Institution Create(string code, string name, string? licenseType = null)
    {
        return new Institution
        {
            InstitutionCode = code,
            InstitutionName = name,
            LicenseType = licenseType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}
