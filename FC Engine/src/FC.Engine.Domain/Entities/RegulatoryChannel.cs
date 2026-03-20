namespace FC.Engine.Domain.Entities;

/// <summary>Registry of supported regulatory submission channels.</summary>
public class RegulatoryChannel
{
    public int Id { get; set; }
    public string RegulatorCode { get; set; } = string.Empty;      // CBN, NDIC, NFIU…
    public string RegulatorName { get; set; } = string.Empty;
    public string PortalName { get; set; } = string.Empty;
    public string IntegrationMethod { get; set; } = "REST";        // REST | SFTP | XML_UPLOAD | SOAP
    public string? BaseUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool RequiresCertificate { get; set; } = true;
    public int? MaxRetriesOverride { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
