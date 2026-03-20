namespace FC.Engine.Domain.Entities;

/// <summary>Cryptographic signature produced for a submission item.</summary>
public class SubmissionSignatureRecord
{
    public long Id { get; set; }
    public long SubmissionItemId { get; set; }
    public int InstitutionId { get; set; }

    /// <summary>SHA-256 thumbprint of the signing certificate.</summary>
    public string CertificateThumbprint { get; set; } = string.Empty;

    /// <summary>e.g. RSA-SHA512 or ECDSA-SHA384.</summary>
    public string SignatureAlgorithm { get; set; } = string.Empty;

    /// <summary>Raw DER-encoded signature bytes.</summary>
    public byte[] SignatureValue { get; set; } = Array.Empty<byte>();

    /// <summary>SHA-512 of the signed payload.</summary>
    public string SignedDataHash { get; set; } = string.Empty;

    public DateTime SignedAt { get; set; }

    /// <summary>RFC 3161 timestamp token from a TSA (optional).</summary>
    public byte[]? TimestampToken { get; set; }

    public bool IsValid { get; set; } = true;

    // Navigation
    public SubmissionItem? SubmissionItem { get; set; }
}
