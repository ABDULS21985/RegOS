using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FC.Engine.Domain.Models;
using FC.Engine.Domain.Models.BatchSubmission;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FC.Engine.Infrastructure.Export.ChannelAdapters;

/// <summary>
/// SEC (Securities and Exchange Commission) direct submission adapter.
/// Transport: REST, API-key authenticated, JSON/multipart hybrid.
/// Handles capital market operator returns (CAR, AIFR, FPR, etc.).
/// R-10: All HTTP calls wrapped in Polly pipeline from base class.
/// </summary>
public sealed class SecChannelAdapter : RegulatoryChannelAdapterBase
{
    private readonly RegulatorApiEndpoint _settings;

    public SecChannelAdapter(
        HttpClient http,
        IOptions<RegulatoryApiSettings> options,
        ILogger<SecChannelAdapter> logger)
        : base(http, logger)
    {
        _settings = options.Value.Sec;
    }

    public override string RegulatorCode => "SEC";
    public override string IntegrationMethod => "REST";

    protected override async Task<BatchRegulatorReceipt> DispatchCoreAsync(
        DispatchPayload payload, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();

        // SEC requires the return file plus a structured JSON header
        var fileContent = new ByteArrayContent(payload.ExportedFileContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            DetectContentType(payload.ExportedFileName));
        content.Add(fileContent, "returnFile", payload.ExportedFileName);

        var header = new
        {
            filerCode = payload.InstitutionCode,
            batchId = payload.BatchReference,
            formType = payload.Metadata.GetValueOrDefault("report_type", "CAR"),
            periodOfReturn = payload.Metadata.GetValueOrDefault("reporting_period", string.Empty),
            numberOfReturns = int.TryParse(
                payload.Metadata.GetValueOrDefault("item_count", "1"), out var n) ? n : 1,
            submissionTimestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            digitalSignature = payload.Signature.SignatureValue.Length > 0
                ? Convert.ToBase64String(payload.Signature.SignatureValue)
                : null,
            signatureAlgorithm = payload.Signature.SignatureAlgorithm,
            certificateThumbprint = payload.Signature.CertificateThumbprint,
            payloadHash = payload.Signature.SignedDataHash
        };
        content.Add(
            new StringContent(JsonSerializer.Serialize(header), Encoding.UTF8, "application/json"),
            "submissionHeader");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v3/submissions");
        request.Headers.TryAddWithoutValidation("X-Api-Key", _settings.ApiKey);
        request.Headers.TryAddWithoutValidation("X-Correlation-Id",
            payload.Metadata.GetValueOrDefault("correlation_id", Guid.NewGuid().ToString()));
        request.Headers.TryAddWithoutValidation("X-Filer-Code", payload.InstitutionCode);
        request.Content = content;

        var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new SubmissionDispatchException(
                $"SEC rejected submission: HTTP {(int)response.StatusCode} — {TruncateBody(body)}",
                (int)response.StatusCode);

        var (reference, _) = ParseJsonReceipt(body);
        Logger.LogInformation(
            "SEC accepted batch {BatchRef}. Reference: {Ref}",
            payload.BatchReference, reference);

        return new BatchRegulatorReceipt(reference, DateTimeOffset.UtcNow, (int)response.StatusCode, body);
    }

    protected override async Task<BatchRegulatorStatusResponse> CheckStatusCoreAsync(
        string receiptReference, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v3/submissions/{Uri.EscapeDataString(receiptReference)}");
        request.Headers.TryAddWithoutValidation("X-Api-Key", _settings.ApiKey);

        var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return new BatchRegulatorStatusResponse(
                receiptReference, "UNKNOWN", BatchSubmissionStatusValue.Submitted, null, DateTimeOffset.UtcNow);

        return ParseSecStatus(body, receiptReference);
    }

    protected override async Task<IReadOnlyList<BatchRegulatorQueryDto>> FetchQueriesCoreAsync(
        string receiptReference, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v3/submissions/{Uri.EscapeDataString(receiptReference)}/clarifications");
        request.Headers.TryAddWithoutValidation("X-Api-Key", _settings.ApiKey);

        var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return [];
        var body = await response.Content.ReadAsStringAsync(ct);
        return ParseJsonQueries(body);
    }

    protected override async Task<BatchRegulatorReceipt> SubmitQueryResponseCoreAsync(
        QueryResponsePayload payload, CancellationToken ct)
    {
        // SEC expects JSON body for clarification responses + separate attachment uploads
        var responseObj = new
        {
            clarificationId = payload.QueryReference,
            responseText = payload.ResponseText,
            responseDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            attachments = payload.Attachments.Select(a => new
            {
                fileName = a.FileName,
                contentType = a.ContentType,
                content = Convert.ToBase64String(a.Content),
                hash = a.FileHash
            }).ToArray()
        };

        using var jsonContent = new StringContent(
            JsonSerializer.Serialize(responseObj), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v3/clarifications/{Uri.EscapeDataString(payload.QueryReference)}/respond");
        request.Headers.TryAddWithoutValidation("X-Api-Key", _settings.ApiKey);
        request.Content = jsonContent;

        var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new SubmissionDispatchException(
                $"SEC clarification response failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        var (reference, _) = ParseJsonReceipt(body);
        return new BatchRegulatorReceipt(reference, DateTimeOffset.UtcNow, (int)response.StatusCode, body);
    }

    private static BatchRegulatorStatusResponse ParseSecStatus(string body, string receiptReference)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // SEC uses "processingStatus" + optional "reviewStatus"
            var rawStatus = root.TryGetProperty("processingStatus", out var ps) ? ps.GetString() ?? "UNKNOWN"
                : root.TryGetProperty("status", out var s) ? s.GetString() ?? "UNKNOWN"
                : "UNKNOWN";

            var message = root.TryGetProperty("remarks", out var r) ? r.GetString()
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
