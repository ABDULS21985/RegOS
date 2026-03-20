using System.Net.Http.Headers;
using System.Text.Json;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Models.BatchSubmission;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace FC.Engine.Infrastructure.Export.ChannelAdapters;

/// <summary>
/// Base class for all regulatory channel adapters.
/// Provides Polly v8 resilience: retry (3×, exponential backoff) + circuit breaker.
/// R-10: Every outbound HTTP call is wrapped — no bare SendAsync.
/// </summary>
public abstract class RegulatoryChannelAdapterBase : IRegulatoryChannelAdapter
{
    protected readonly HttpClient Http;
    protected readonly ILogger Logger;

    private readonly ResiliencePipeline _pipeline;

    protected RegulatoryChannelAdapterBase(HttpClient http, ILogger logger)
    {
        Http = http;
        Logger = logger;
        _pipeline = BuildResiliencePipeline();
    }

    public abstract string RegulatorCode { get; }
    public abstract string IntegrationMethod { get; }

    public async Task<BatchRegulatorReceipt> DispatchAsync(
        DispatchPayload payload, CancellationToken ct = default)
    {
        return await _pipeline.ExecuteAsync(
            async token => await DispatchCoreAsync(payload, token), ct);
    }

    public async Task<BatchRegulatorStatusResponse> CheckStatusAsync(
        string receiptReference, CancellationToken ct = default)
    {
        return await _pipeline.ExecuteAsync(
            async token => await CheckStatusCoreAsync(receiptReference, token), ct);
    }

    public async Task<IReadOnlyList<BatchRegulatorQueryDto>> FetchQueriesAsync(
        string receiptReference, CancellationToken ct = default)
    {
        return await _pipeline.ExecuteAsync(
            async token => await FetchQueriesCoreAsync(receiptReference, token), ct);
    }

    public async Task<BatchRegulatorReceipt> SubmitQueryResponseAsync(
        QueryResponsePayload payload, CancellationToken ct = default)
    {
        return await _pipeline.ExecuteAsync(
            async token => await SubmitQueryResponseCoreAsync(payload, token), ct);
    }

    // Subclasses implement these:
    protected abstract Task<BatchRegulatorReceipt> DispatchCoreAsync(
        DispatchPayload payload, CancellationToken ct);

    protected abstract Task<BatchRegulatorStatusResponse> CheckStatusCoreAsync(
        string receiptReference, CancellationToken ct);

    protected abstract Task<IReadOnlyList<BatchRegulatorQueryDto>> FetchQueriesCoreAsync(
        string receiptReference, CancellationToken ct);

    protected abstract Task<BatchRegulatorReceipt> SubmitQueryResponseCoreAsync(
        QueryResponsePayload payload, CancellationToken ct);

    // ── Resilience pipeline (Polly v8) ─────────────────────────────────

    private ResiliencePipeline BuildResiliencePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(5),
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<SubmissionDispatchException>(ex =>
                        ex.HttpStatusCode is 429 or 503 or 502 or 504),
                OnRetry = args =>
                {
                    Logger.LogWarning(
                        "Retry {Attempt} for {Regulator} after {Delay}: {Reason}",
                        args.AttemptNumber + 1, RegulatorCode,
                        args.RetryDelay, args.Outcome.Exception?.Message);
                    return default;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.6,
                SamplingDuration = TimeSpan.FromMinutes(2),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromMinutes(1),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<SubmissionDispatchException>(),
                OnOpened = args =>
                {
                    Logger.LogError(
                        "Circuit breaker OPEN for {Regulator}. Break duration: {Duration}",
                        RegulatorCode, args.BreakDuration);
                    return default;
                },
                OnClosed = args =>
                {
                    Logger.LogInformation("Circuit breaker CLOSED for {Regulator}", RegulatorCode);
                    return default;
                }
            })
            .Build();
    }

    // ── Shared parsing helpers ─────────────────────────────────────────

    protected static (string Reference, string Message) ParseJsonReceipt(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var reference = root.TryGetProperty("referenceNumber", out var r) ? r.GetString() ?? string.Empty
                : root.TryGetProperty("reference", out var r2) ? r2.GetString() ?? string.Empty
                : string.Empty;
            var message = root.TryGetProperty("message", out var m) ? m.GetString() ?? "Accepted" : "Accepted";
            return (reference, message);
        }
        catch
        {
            return (string.Empty, "Accepted (response parse failed)");
        }
    }

    protected static BatchRegulatorStatusResponse ParseJsonStatus(
        string body, string receiptReference, BatchSubmissionStatusValue fallback = BatchSubmissionStatusValue.Submitted)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var rawStatus = root.TryGetProperty("status", out var s) ? s.GetString() ?? "UNKNOWN" : "UNKNOWN";
            return new BatchRegulatorStatusResponse(
                ReceiptReference: receiptReference,
                RegulatorStatusCode: rawStatus,
                MappedStatus: MapStatus(rawStatus),
                StatusMessage: root.TryGetProperty("message", out var m) ? m.GetString() : null,
                LastUpdated: DateTimeOffset.UtcNow);
        }
        catch
        {
            return new BatchRegulatorStatusResponse(
                receiptReference, "UNKNOWN", fallback, null, DateTimeOffset.UtcNow);
        }
    }

    protected static BatchSubmissionStatusValue MapStatus(string status) =>
        status.ToUpperInvariant() switch
        {
            "RECEIVED" or "ACKNOWLEDGED" => BatchSubmissionStatusValue.Acknowledged,
            "UNDER_REVIEW" or "PROCESSING" or "IN_PROGRESS" => BatchSubmissionStatusValue.Processing,
            "ACCEPTED" => BatchSubmissionStatusValue.Accepted,
            "FINAL_ACCEPTED" or "APPROVED" => BatchSubmissionStatusValue.FinalAccepted,
            "QUERIES_RAISED" or "CLARIFICATION_NEEDED" => BatchSubmissionStatusValue.QueriesRaised,
            "REJECTED" => BatchSubmissionStatusValue.Rejected,
            _ => BatchSubmissionStatusValue.Submitted
        };

    protected static List<BatchRegulatorQueryDto> ParseJsonQueries(string body)
    {
        var result = new List<BatchRegulatorQueryDto>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            var arr = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray()
                : doc.RootElement.TryGetProperty("queries", out var q)
                    ? q.EnumerateArray()
                    : Enumerable.Empty<JsonElement>();

            foreach (var item in arr)
            {
                result.Add(new BatchRegulatorQueryDto(
                    QueryReference: item.TryGetProperty("id", out var id) ? id.ToString() : Guid.NewGuid().ToString(),
                    Type: item.TryGetProperty("type", out var t) ? t.GetString() ?? "CLARIFICATION" : "CLARIFICATION",
                    QueryText: item.TryGetProperty("text", out var tx) ? tx.GetString() ?? string.Empty : string.Empty,
                    DueDate: item.TryGetProperty("dueDate", out var dd) && DateOnly.TryParse(dd.GetString(), out var d) ? d : null,
                    Priority: item.TryGetProperty("priority", out var p) ? p.GetString() ?? "NORMAL" : "NORMAL"));
            }
        }
        catch { /* return empty */ }
        return result;
    }

    protected static string TruncateBody(string body, int maxLen = 500)
        => body.Length > maxLen ? body[..maxLen] + "…" : body;
}

/// <summary>Exception thrown when a regulator rejects or fails to process a dispatch.</summary>
public sealed class SubmissionDispatchException : Exception
{
    public int HttpStatusCode { get; }

    public SubmissionDispatchException(string message, int httpStatusCode = 0)
        : base(message)
    {
        HttpStatusCode = httpStatusCode;
    }
}
