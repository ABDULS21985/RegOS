using FC.Engine.Domain.Models.BatchSubmission;

namespace FC.Engine.Domain.Abstractions;

/// <summary>
/// Signs submission payloads using the institution's X.509 certificate.
/// Includes optional RFC 3161 timestamping from a configured TSA.
/// </summary>
public interface ISubmissionSigningService
{
    Task<BatchSignatureInfo> SignPayloadAsync(
        int institutionId,
        byte[] payloadContent,
        CancellationToken ct = default);

    Task<bool> VerifySignatureAsync(
        BatchSignatureInfo signature,
        byte[] payloadContent,
        CancellationToken ct = default);
}
