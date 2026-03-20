namespace FC.Engine.Domain.Models.BatchSubmission;

public sealed record BatchSignatureInfo(
    string CertificateThumbprint,
    string SignatureAlgorithm,   // "RSA-SHA512" | "ECDSA-SHA384"
    byte[] SignatureValue,
    string SignedDataHash,       // SHA-512 hex
    DateTimeOffset SignedAt,
    byte[]? TimestampToken       // RFC 3161 DER-encoded token
);
