using System.Security.Claims;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Models.BatchSubmission;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Api.Endpoints;

/// <summary>
/// RG-34 batch submission endpoints.
/// R-05: All routes tenant-isolated via InstitutionId from claims.
/// </summary>
public static class SubmissionBatchEndpoints
{
    public static void MapSubmissionBatchEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/submission-batches")
            .WithTags("SubmissionBatches")
            .RequireAuthorization("InstitutionApi");

        // POST /submission-batches — initiate a new batch submission
        group.MapPost("/", async (
            SubmitBatchRequest request,
            ISubmissionOrchestrator orchestrator,
            ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            var institutionId = ResolveInstitutionId(principal);
            if (institutionId == 0) return Results.Forbid();

            var userId = ResolveUserId(principal);
            var result = await orchestrator.SubmitBatchAsync(
                institutionId, request.RegulatorCode,
                request.SubmissionIds, userId, ct);

            return result.Success
                ? Results.Ok(result)
                : Results.UnprocessableEntity(result);
        })
        .RequireAuthorization("CanDirectSubmit")
        .WithName("SubmitBatch")
        .WithSummary("Initiate a new batch submission to a regulator");

        // GET /submission-batches — list batches for current institution
        group.MapGet("/", async (
            string? regulatorCode,
            string? status,
            int page,
            int pageSize,
            MetadataDbContext db,
            ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            var institutionId = ResolveInstitutionId(principal);
            if (institutionId == 0) return Results.Forbid();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = db.SubmissionBatches
                .AsNoTracking()
                .Where(b => b.InstitutionId == institutionId);

            if (!string.IsNullOrWhiteSpace(regulatorCode))
                query = query.Where(b => b.RegulatorCode == regulatorCode.ToUpperInvariant());

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(b => b.Status == status.ToUpperInvariant());

            var total = await query.CountAsync(ct);
            var batches = await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    b.Id,
                    b.BatchReference,
                    b.RegulatorCode,
                    b.Status,
                    ItemCount = b.Items.Count,
                    b.SubmittedAt,
                    b.AcknowledgedAt,
                    b.FinalStatusAt,
                    b.RetryCount,
                    LatestReceipt = b.Receipts
                        .OrderByDescending(r => r.ReceivedAt)
                        .Select(r => r.ReceiptReference)
                        .FirstOrDefault(),
                    OpenQueries = b.Queries.Count(q => q.Status == "OPEN" || q.Status == "IN_PROGRESS"),
                    b.CreatedAt
                })
                .ToListAsync(ct);

            return Results.Ok(new { Total = total, Page = page, PageSize = pageSize, Items = batches });
        })
        .RequireAuthorization("CanViewDirectStatus")
        .WithName("ListSubmissionBatches")
        .WithSummary("List submission batches for the current institution");

        // GET /submission-batches/{batchId} — batch detail
        group.MapGet("/{batchId:long}", async (
            long batchId,
            MetadataDbContext db,
            ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            var institutionId = ResolveInstitutionId(principal);
            if (institutionId == 0) return Results.Forbid();

            var batch = await db.SubmissionBatches
                .AsNoTracking()
                .Include(b => b.Items)
                .Include(b => b.Receipts)
                .Include(b => b.Queries.Where(q => q.Status != "CLOSED"))
                .Include(b => b.AuditLogs.OrderByDescending(a => a.PerformedAt).Take(20))
                .FirstOrDefaultAsync(b => b.Id == batchId && b.InstitutionId == institutionId, ct);

            if (batch is null) return Results.NotFound();

            return Results.Ok(batch);
        })
        .RequireAuthorization("CanViewDirectStatus")
        .WithName("GetSubmissionBatch")
        .WithSummary("Get submission batch detail");

        // POST /submission-batches/{batchId}/retry — retry a failed batch
        group.MapPost("/{batchId:long}/retry", async (
            long batchId,
            ISubmissionOrchestrator orchestrator,
            ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            var institutionId = ResolveInstitutionId(principal);
            if (institutionId == 0) return Results.Forbid();

            var result = await orchestrator.RetryBatchAsync(institutionId, batchId, ct);
            return result.Success ? Results.Ok(result) : Results.UnprocessableEntity(result);
        })
        .RequireAuthorization("CanDirectSubmit")
        .WithName("RetrySubmissionBatch")
        .WithSummary("Retry a failed submission batch");

        // POST /submission-batches/{batchId}/refresh — manually refresh status from regulator
        group.MapPost("/{batchId:long}/refresh", async (
            long batchId,
            ISubmissionOrchestrator orchestrator,
            ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            var institutionId = ResolveInstitutionId(principal);
            if (institutionId == 0) return Results.Forbid();

            var result = await orchestrator.RefreshStatusAsync(institutionId, batchId, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization("CanViewDirectStatus")
        .WithName("RefreshBatchStatus")
        .WithSummary("Manually refresh batch status from regulator");
    }

    private static int ResolveInstitutionId(ClaimsPrincipal principal)
    {
        return ApiClaimResolvers.GetInstitutionId(principal);
    }

    private static int ResolveUserId(ClaimsPrincipal principal)
    {
        return ApiClaimResolvers.GetUserId(principal);
    }
}

public sealed record SubmitBatchRequest(
    string RegulatorCode,
    IReadOnlyList<int> SubmissionIds);
