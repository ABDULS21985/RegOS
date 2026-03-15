using System.Security.Claims;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Models.BatchSubmission;

namespace FC.Engine.Api.Endpoints;

/// <summary>
/// Endpoints for managing regulator queries (RG-34).
/// R-05: All routes tenant-isolated via InstitutionId from claims.
/// </summary>
public static class RegulatoryQueryEndpoints
{
    public static void MapRegulatoryQueryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/regulatory-queries")
            .WithTags("RegulatoryQueries")
            .RequireAuthorization("InstitutionApi");

        // GET /regulatory-queries — list open queries for institution
        group.MapGet("/", async (
            string? regulatorCode,
            int page,
            int pageSize,
            IRegulatorQueryService queryService,
            ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            var institutionId = ResolveInstitutionId(principal);
            if (institutionId == 0) return Results.Forbid();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var queries = await queryService.GetOpenQueriesAsync(
                institutionId, regulatorCode, page, pageSize, ct);

            return Results.Ok(queries);
        })
        .RequireAuthorization("CanViewDirectStatus")
        .WithName("GetOpenRegulatoryQueries")
        .WithSummary("List open regulatory queries for the current institution");

        // POST /regulatory-queries/{queryId}/assign — assign query to a team member
        group.MapPost("/{queryId:long}/assign", async (
            long queryId,
            AssignQueryRequest request,
            IRegulatorQueryService queryService,
            ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            var institutionId = ResolveInstitutionId(principal);
            if (institutionId == 0) return Results.Forbid();

            await queryService.AssignQueryAsync(institutionId, queryId, request.AssignToUserId, ct);
            return Results.NoContent();
        })
        .RequireAuthorization("CanDirectSubmit")
        .WithName("AssignRegulatoryQuery")
        .WithSummary("Assign a regulatory query to a team member");

        // POST /regulatory-queries/{queryId}/respond — submit response with optional attachments
        group.MapPost("/{queryId:long}/respond", async (
            long queryId,
            HttpRequest httpRequest,
            IRegulatorQueryService queryService,
            ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            var institutionId = ResolveInstitutionId(principal);
            if (institutionId == 0) return Results.Forbid();

            var userId = ResolveUserId(principal);

            // Parse response text from form field
            var responseText = httpRequest.Form.TryGetValue("responseText", out var rt)
                ? rt.ToString()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(responseText))
                return Results.BadRequest(new { error = "responseText is required." });

            // Build attachments from uploaded files
            var attachments = new List<AttachmentPayload>();
            foreach (var file in httpRequest.Form.Files)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                var content = ms.ToArray();
                var hash = Convert.ToHexString(
                    System.Security.Cryptography.SHA512.HashData(content)).ToLowerInvariant();

                attachments.Add(new AttachmentPayload(
                    file.FileName,
                    file.ContentType,
                    content,
                    hash));
            }

            var responseId = await queryService.SubmitResponseAsync(
                institutionId, queryId, responseText, attachments, userId, ct);

            return Results.Ok(new { ResponseId = responseId });
        })
        .RequireAuthorization("CanDirectSubmit")
        .DisableAntiforgery()
        .WithName("SubmitQueryResponse")
        .WithSummary("Submit a response to a regulatory query");
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

public sealed record AssignQueryRequest(int AssignToUserId);
