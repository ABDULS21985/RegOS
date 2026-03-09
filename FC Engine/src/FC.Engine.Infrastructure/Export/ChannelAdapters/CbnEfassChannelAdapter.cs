using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FC.Engine.Domain.Models;
using FC.Engine.Domain.Models.BatchSubmission;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FC.Engine.Infrastructure.Export.ChannelAdapters;

/// <summary>
/// CBN eFASS direct submission adapter.
/// Transport: REST (OAuth2 client-credentials + multipart/form-data).
/// Package format: ZIP containing xlsx + xml + manifest.json.
/// R-10: All HTTP calls wrapped in the Polly resilience pipeline from base class.
/// </summary>
public sealed class CbnEfassChannelAdapter : RegulatoryChannelAdapterBase
{
    private readonly CbnApiSettings _settings;

    public CbnEfassChannelAdapter(
        HttpClient http,
        IOptions<RegulatoryApiSettings> options,
        ILogger<CbnEfassChannelAdapter> logger)
        : base(http, logger)
    {
        _settings = options.Value.Cbn;
    }

    public override string RegulatorCode => "CBN";
    public override string IntegrationMethod => "REST";

    protected override async Task<BatchRegulatorReceipt> DispatchCoreAsync(
        DispatchPayload payload, CancellationToken ct)
    {
        var token = await AuthenticateAsync(ct);

        // Build ZIP package: xlsx/xml export + evidence + manifest
        var zipBytes = BuildCbnPackage(payload);

        using var content = new MultipartFormDataContent();
        var pkgContent = new ByteArrayContent(zipBytes);
        pkgContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(pkgContent, "package",
            $"{payload.InstitutionCode}_{payload.BatchReference}.zip");

        if (payload.Signature.SignatureValue.Length > 0)
        {
            content.Add(new ByteArrayContent(payload.Signature.SignatureValue),
                "signature", "signature.der");
            content.Add(new StringContent(payload.Signature.SignedDataHash),
                "signatureHash");
            content.Add(new StringContent(payload.Signature.CertificateThumbprint),
                "certThumbprint");
        }

        content.Add(new StringContent(payload.InstitutionCode), "institutionCode");
        content.Add(new StringContent(payload.Metadata.GetValueOrDefault("reporting_period", "")), "period");
        content.Add(new StringContent(payload.Metadata.GetValueOrDefault("correlation_id", "")), "correlationId");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/submissions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;

        var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new SubmissionDispatchException(
                $"CBN eFASS rejected submission: HTTP {(int)response.StatusCode} — {TruncateBody(body)}",
                (int)response.StatusCode);
        }

        var (reference, message) = ParseJsonReceipt(body);
        Logger.LogInformation(
            "CBN eFASS accepted batch {BatchRef}. Reference: {Reference}",
            payload.BatchReference, reference);

        return new BatchRegulatorReceipt(
            reference,
            DateTimeOffset.UtcNow,
            (int)response.StatusCode,
            body);
    }

    protected override async Task<BatchRegulatorStatusResponse> CheckStatusCoreAsync(
        string receiptReference, CancellationToken ct)
    {
        var token = await AuthenticateAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/submissions/{Uri.EscapeDataString(receiptReference)}/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return new BatchRegulatorStatusResponse(receiptReference, "UNKNOWN",
                BatchSubmissionStatusValue.Submitted, $"Status check failed: {response.StatusCode}", DateTimeOffset.UtcNow);

        return ParseJsonStatus(body, receiptReference);
    }

    protected override async Task<IReadOnlyList<BatchRegulatorQueryDto>> FetchQueriesCoreAsync(
        string receiptReference, CancellationToken ct)
    {
        var token = await AuthenticateAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/submissions/{Uri.EscapeDataString(receiptReference)}/queries");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return [];
        var body = await response.Content.ReadAsStringAsync(ct);
        return ParseJsonQueries(body);
    }

    protected override async Task<BatchRegulatorReceipt> SubmitQueryResponseCoreAsync(
        QueryResponsePayload payload, CancellationToken ct)
    {
        var token = await AuthenticateAsync(ct);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(payload.QueryReference), "queryReference");
        content.Add(new StringContent(payload.ResponseText), "responseText");
        foreach (var att in payload.Attachments)
        {
            var attContent = new ByteArrayContent(att.Content);
            attContent.Headers.ContentType = new MediaTypeHeaderValue(att.ContentType);
            content.Add(attContent, "attachments", att.FileName);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/queries/{Uri.EscapeDataString(payload.QueryReference)}/respond");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;

        var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new SubmissionDispatchException(
                $"CBN query response failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        var (reference, _) = ParseJsonReceipt(body);
        return new BatchRegulatorReceipt(reference, DateTimeOffset.UtcNow, (int)response.StatusCode, body);
    }

    // ── Private ────────────────────────────────────────────────────────

    private async Task<string> AuthenticateAsync(CancellationToken ct)
    {
        using var authBody = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret
        });

        var authResponse = await Http.PostAsync("/api/v1/auth/token", authBody, ct);
        var authJson = await authResponse.Content.ReadAsStringAsync(ct);

        if (!authResponse.IsSuccessStatusCode)
            throw new SubmissionDispatchException($"CBN eFASS auth failed: {authJson}", (int)authResponse.StatusCode);

        using var doc = JsonDocument.Parse(authJson);
        return doc.RootElement.GetProperty("access_token").GetString()
               ?? throw new InvalidOperationException("CBN eFASS auth response missing access_token.");
    }

    private static byte[] BuildCbnPackage(DispatchPayload payload)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Primary export file
            var exportEntry = archive.CreateEntry(payload.ExportedFileName);
            using (var es = exportEntry.Open())
                es.Write(payload.ExportedFileContent);

            // Evidence package (if available)
            if (payload.EvidencePackage?.Length > 0)
            {
                var evidenceEntry = archive.CreateEntry("evidence.zip");
                using var evStream = evidenceEntry.Open();
                evStream.Write(payload.EvidencePackage);
            }

            // Manifest
            var manifest = JsonSerializer.Serialize(new
            {
                regulator = "CBN",
                batchReference = payload.BatchReference,
                institutionCode = payload.InstitutionCode,
                exportFormat = payload.ExportFormat,
                payloadHash = payload.Digest.HashValue,
                payloadSize = payload.Digest.PayloadSizeBytes,
                generatedAt = DateTimeOffset.UtcNow.ToString("o"),
                signature = new
                {
                    algorithm = payload.Signature.SignatureAlgorithm,
                    thumbprint = payload.Signature.CertificateThumbprint,
                    signedAt = payload.Signature.SignedAt.ToString("o")
                }
            }, new JsonSerializerOptions { WriteIndented = true });

            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var mStream = manifestEntry.Open())
            {
                var manifestBytes = Encoding.UTF8.GetBytes(manifest);
                mStream.Write(manifestBytes);
            }
        }

        return ms.ToArray();
    }
}
