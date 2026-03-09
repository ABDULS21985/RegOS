namespace FC.Engine.Domain.Models.BatchSubmission;

public sealed record PayloadDigest(
    string Algorithm,       // "SHA-512"
    string HashValue,       // lowercase hex
    long PayloadSizeBytes);
