using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FC.Engine.Domain.Models;
using FC.Engine.Domain.Models.BatchSubmission;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FC.Engine.Infrastructure.Export.ChannelAdapters;

/// <summary>
/// PenCom (National Pension Commission) direct submission adapter.
/// Transport: REST, API-key authenticated, multipart/form-data.
/// Handles pension fund returns (PFA monthly contributions, PFO annual reports, etc.).
/// R-10: All HTTP calls wrapped in Polly pipeline from base class.
/// </summary>
public sealed class PencomChannelAdapter : RegulatoryChannelAdapterBase
{
    private readonly RegulatorApiEndpoint _settings;

    public PencomChannelAdapter(
        HttpClient http,
        IOptions<RegulatoryApiSettings> options,
        ILogger<PencomChannelAdapter> logger)
        : base(http, logger)
    {
        _settings = options.Value.Pencom;
    }

    public override string RegulatorCode => "PENCOM";
    public override string IntegrationMethod => "REST";

    protected override async Task<BatchRegulatorReceipt> DispatchCoreAsync(
        DispatchPayload payload, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(payload.ExportedFileContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            DetectContentType(payload.ExportedFileName));
        content.Add(fileContent, "returnFile", payload.ExportedFileName);

        // PenCom distinguishes PFA (fund administrator) vs PFO (fund operator) submissions
        var meta = new
        {
            pfaCode = payload.InstitutionCode,
            batchRef = payload.BatchReference,
            returnType = payload.Metadata.GetValueOrDefault("report_type", "MONTHLY_CONTRIBUTION"),
            reportingPeriod = payload.Metadata.GetValueOrDefault("reporting_period", string.Empty),
            contributionCount = int.TryParse(
                payload.Metadata.GetValueOrDefault("item_count", "1"), out var n) ? n : 1,
            submittedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            hashAlgorithm = payload.Signature.SignatureAlgorithm,
            payloadHash = payload.Signature.SignedDataHash
        };
        content.Add(
            new StringContent(JsonSerializer.Serialize(meta), Encoding.UTF8, "application/json"),
            "submissionMeta");

        if (payload.Signature.SignatureValue.Length > 0)
        {
            content.Add(
                new StringContent(Convert.ToBase64String(payload.Signature.SignatureValue)),
                "digitalSignature");
            content.Add(
                new StringContent(payload.Signature.CertificateThumbprint),
                "certThumbprint");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/pension-returns/submit");
        request.Headers.TryAddWithoutValidation("X-Api-Key", _settings.ApiKey);
        request.Headers.TryAddWithoutValidation("X-Correlation-Id",
            payload.Metadata.GetValueOrDefault("correlation_id", Guid.NewGuid().ToString()));
        request.Headers.TryAddWithoutValidation("X-PFA-Code", payload.InstitutionCode);
        request.Content = content;

        var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new SubmissionDispatchException(
                $"PenCom rejected submission: HTTP {(int)response.StatusCode} — {TruncateBody(body)}",
                (int)response.StatusCode);

        var (reference, _) = ParseJsonReceipt(body);
        Logger.LogInformation(
            "PenCom accepted batch {BatchRef}. Reference: {Ref}",
            payload.BatchReference, reference);

        return new BatchRegulatorReceipt(reference, DateTimeOffset.UtcNow, (int)response.StatusCode, body);
    }

    protected override async Task<BatchRegulatorStatusResponse> CheckStatusCoreAsync(
        string receiptReference, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v2/pension-returns/{Uri.EscapeDataString(receiptReference)}/status");
        request.Headers.TryAddWithoutValidation("X-Api-Key", _settings.ApiKey);

        var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return new BatchRegulatorStatusResponse(
                receiptReference, "UNKNOWN", BatchSubmissionStatusValue.Submitted, null, DateTimeOffset.UtcNow);

        return ParsePencomStatus(body, receiptReference);
    }

    protected override async Task<IReadOnlyList<BatchRegulatorQueryDto>> FetchQueriesCoreAsync(
        string receiptReference, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v2/pension-returns/{Uri.EscapeDataString(receiptReference)}/queries");
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
        content.Add(new StringContent(payload.QueryReference), "queryReference");
        content.Add(new StringContent(payload.ResponseText, Encoding.UTF8, "text/plain"), "responseNarrative");
        content.Add(
            new StringContent(DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
            "responseDate");

        foreach (var attachment in payload.Attachments)
        {
            var ac = new ByteArrayContent(attachment.Content);
            ac.Headers.ContentType = new MediaTypeHeaderValue(attachment.ContentType);
            content.Add(ac, "evidenceDocuments", attachment.FileName);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v2/queries/{Uri.EscapeDataString(payload.QueryReference)}/respond");
        request.Headers.TryAddWithoutValidation("X-Api-Key", _settings.ApiKey);
        request.Content = content;

        var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new SubmissionDispatchException(
                $"PenCom query response failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        var (reference, _) = ParseJsonReceipt(body);
        return new BatchRegulatorReceipt(reference, DateTimeOffset.UtcNow, (int)response.StatusCode, body);
    }

    private static BatchRegulatorStatusResponse ParsePencomStatus(string body, string receiptReference)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // PenCom uses "submissionStatus" or generic "status"
            var rawStatus = root.TryGetProperty("submissionStatus", out var ss) ? ss.GetString() ?? "UNKNOWN"
                : root.TryGetProperty("status", out var s) ? s.GetString() ?? "UNKNOWN"
                : "UNKNOWN";

            var message = root.TryGetProperty("reviewRemarks", out var rr) ? rr.GetString()
                : root.TryGetProperty("message", out var m) ? m.GetString()
                : null;

            return new BatchRegulatorStatusResponse(
                receiptReference, rawStatus, MapStatus(rawStatus), message, DateTimeOffset.UtcNow);
        }
        catch
        {
            return new BatchRegulatorStatusResponse(
                receiptReference, "UNKNOWN", BatchSubmissionStatusValue.Submitted, null, DateTimeOffset.UtcNow);
        }
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
