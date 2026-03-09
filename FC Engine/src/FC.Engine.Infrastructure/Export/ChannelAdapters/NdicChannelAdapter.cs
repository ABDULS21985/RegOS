using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FC.Engine.Domain.Models;
using FC.Engine.Domain.Models.BatchSubmission;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FC.Engine.Infrastructure.Export.ChannelAdapters;

/// <summary>
/// NDIC (Nigeria Deposit Insurance Corporation) direct submission adapter.
/// Transport: REST, API-key authenticated, multipart/form-data.
/// Handles premium remittance and failed bank returns.
/// R-10: All HTTP calls wrapped in Polly pipeline from base class.
/// </summary>
public sealed class NdicChannelAdapter : RegulatoryChannelAdapterBase
{
    private readonly RegulatorApiEndpoint _settings;

    public NdicChannelAdapter(
        HttpClient http,
        IOptions<RegulatoryApiSettings> options,
        ILogger<NdicChannelAdapter> logger)
        : base(http, logger)
    {
        _settings = options.Value.Ndic;
    }

    public override string RegulatorCode => "NDIC";
    public override string IntegrationMethod => "REST";

    protected override async Task<BatchRegulatorReceipt> DispatchCoreAsync(
        DispatchPayload payload, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(payload.ExportedFileContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            DetectContentType(payload.ExportedFileName));
        content.Add(fileContent, "returnFile", payload.ExportedFileName);

        // Submission metadata
        var meta = new
        {
            institutionCode = payload.InstitutionCode,
            batchReference = payload.BatchReference,
            reportType = payload.Metadata.GetValueOrDefault("report_type", "PREMIUM_REMITTANCE"),
            reportingPeriod = payload.Metadata.GetValueOrDefault("reporting_period", string.Empty),
            itemCount = payload.Metadata.GetValueOrDefault("item_count", "1")
        };
        content.Add(new StringContent(JsonSerializer.Serialize(meta), Encoding.UTF8, "application/json"), "metadata");

        if (payload.Signature.SignatureValue.Length > 0)
        {
            content.Add(
                new StringContent(Convert.ToBase64String(payload.Signature.SignatureValue)),
                "digitalSignature");
            content.Add(
                new StringContent(payload.Signature.CertificateThumbprint),
                "certificateThumbprint");
            content.Add(
                new StringContent(payload.Signature.SignedDataHash),
                "dataHash");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/returns/submit");
        request.Headers.TryAddWithoutValidation("X-Api-Key", _settings.ApiKey);
        request.Headers.TryAddWithoutValidation("X-Correlation-Id",
            payload.Metadata.GetValueOrDefault("correlation_id", Guid.NewGuid().ToString()));
        request.Headers.TryAddWithoutValidation("X-Institution-Code", payload.InstitutionCode);
        request.Content = content;

        var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new SubmissionDispatchException(
                $"NDIC rejected submission: HTTP {(int)response.StatusCode} — {TruncateBody(body)}",
                (int)response.StatusCode);

        var (reference, _) = ParseJsonReceipt(body);
        Logger.LogInformation(
            "NDIC accepted batch {BatchRef}. Reference: {Ref}",
            payload.BatchReference, reference);

        return new BatchRegulatorReceipt(reference, DateTimeOffset.UtcNow, (int)response.StatusCode, body);
    }

    protected override async Task<BatchRegulatorStatusResponse> CheckStatusCoreAsync(
        string receiptReference, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v2/returns/{Uri.EscapeDataString(receiptReference)}/status");
        request.Headers.TryAddWithoutValidation("X-Api-Key", _settings.ApiKey);

        var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return new BatchRegulatorStatusResponse(
                receiptReference, "UNKNOWN", BatchSubmissionStatusValue.Submitted, null, DateTimeOffset.UtcNow);

        return ParseJsonStatus(body, receiptReference);
    }

    protected override async Task<IReadOnlyList<BatchRegulatorQueryDto>> FetchQueriesCoreAsync(
        string receiptReference, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v2/returns/{Uri.EscapeDataString(receiptReference)}/queries");
        request.Headers.TryAddWithoutValidation("X-Api-Key", _settings.ApiKey);

        var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return [];
        var body = await response.Content.ReadAsStringAsync(ct);
        return ParseJsonQueries(body);
    }

    protected override async Task<BatchRegulatorReceipt> SubmitQueryResponseCoreAsync(
        QueryResponsePayload payload, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(payload.ResponseText, Encoding.UTF8, "text/plain"), "responseText");
        content.Add(new StringContent(payload.QueryReference), "queryReference");

        foreach (var attachment in payload.Attachments)
        {
            var ac = new ByteArrayContent(attachment.Content);
            ac.Headers.ContentType = new MediaTypeHeaderValue(attachment.ContentType);
            content.Add(ac, "attachments", attachment.FileName);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v2/queries/{Uri.EscapeDataString(payload.QueryReference)}/respond");
        request.Headers.TryAddWithoutValidation("X-Api-Key", _settings.ApiKey);
        request.Content = content;

        var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new SubmissionDispatchException(
                $"NDIC query response failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        var (reference, _) = ParseJsonReceipt(body);
        return new BatchRegulatorReceipt(reference, DateTimeOffset.UtcNow, (int)response.StatusCode, body);
    }

    private static string DetectContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xml"  => "application/xml",
            ".csv"  => "text/csv",
            ".zip"  => "application/zip",
            _       => "application/octet-stream"
        };
}
