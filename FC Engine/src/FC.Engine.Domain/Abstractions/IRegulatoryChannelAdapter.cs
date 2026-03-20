using FC.Engine.Domain.Models.BatchSubmission;

namespace FC.Engine.Domain.Abstractions;

/// <summary>
/// Regulator-specific transport adapter. One implementation per regulator.
/// Registered in DI as keyed services using RegulatorCode as the key.
/// </summary>
public interface IRegulatoryChannelAdapter
{
    string RegulatorCode { get; }
    string IntegrationMethod { get; }    // REST | SFTP | XML_UPLOAD | SOAP

    Task<BatchRegulatorReceipt> DispatchAsync(
        DispatchPayload payload, CancellationToken ct = default);

    Task<BatchRegulatorStatusResponse> CheckStatusAsync(
        string receiptReference, CancellationToken ct = default);

    Task<IReadOnlyList<BatchRegulatorQueryDto>> FetchQueriesAsync(
        string receiptReference, CancellationToken ct = default);

    Task<BatchRegulatorReceipt> SubmitQueryResponseAsync(
        QueryResponsePayload payload, CancellationToken ct = default);
}
