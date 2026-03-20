namespace FC.Engine.Domain.Entities;

/// <summary>An individual return within a submission batch.</summary>
public class SubmissionItem
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public int InstitutionId { get; set; }

    /// <summary>FK to Submissions table (RG-11 return instance).</summary>
    public int SubmissionId { get; set; }

    public string ReturnCode { get; set; } = string.Empty;         // SRF-001, MBRF-300…
    public int ReturnVersion { get; set; }
    public string ReportingPeriod { get; set; } = string.Empty;    // 2026-Q1, 2026-02
    public string ExportFormat { get; set; } = string.Empty;       // XBRL | XML | CSV | XLSX
    public string ExportPayloadHash { get; set; } = string.Empty;  // SHA-512 hex
    public long ExportPayloadSize { get; set; }
    public long? EvidencePackageId { get; set; }
    public string Status { get; set; } = "PENDING";
    public string RegulatorCode { get; set; } = string.Empty;
    public string? RegulatorReference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public SubmissionBatch? Batch { get; set; }
    public ICollection<SubmissionSignatureRecord> Signatures { get; set; } = new List<SubmissionSignatureRecord>();
}
