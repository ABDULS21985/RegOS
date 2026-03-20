using FC.Engine.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace FC.Engine.Api.Endpoints;

public static class DataFeedEndpoints
{
    public static void MapDataFeedEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/returns").WithTags("Data Feed");

        group.MapPost("/{returnCode}/data", async (
            string returnCode,
            [FromBody] DataFeedRequest request,
            [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
            IDataFeedService dataFeedService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.CurrentTenantId.HasValue)
            {
                return Results.Forbid();
            }

            if (request is null)
            {
                return Results.BadRequest(new { error = "Request payload is required." });
            }

            var tenantId = tenantContext.CurrentTenantId.Value;
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existing = await dataFeedService.GetByIdempotencyKey(tenantId, idempotencyKey, ct);
                if (existing is not null)
                {
                    return Results.Ok(existing);
                }
            }

            var result = await dataFeedService.ProcessFeed(tenantId, returnCode, request, idempotencyKey, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .RequireAuthorization("CanCreateSubmission")
        .WithName("DataFeed")
        .WithSummary("Submit return data payload for integration feeds with idempotency.");

        group.MapGet("/{returnCode}/mappings/{integrationName}", async (
            string returnCode,
            string integrationName,
            IDataFeedService dataFeedService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.CurrentTenantId.HasValue)
            {
                return Results.Forbid();
            }

            var mappings = await dataFeedService.GetFieldMappings(
                tenantContext.CurrentTenantId.Value,
                integrationName,
                returnCode,
                ct);
            return Results.Ok(mappings);
        })
        .RequireAuthorization("CanViewSubmissions")
        .WithSummary("Get tenant field mappings for an integration/return.");

        group.MapPut("/{returnCode}/mappings/{integrationName}", async (
            string returnCode,
            string integrationName,
            [FromBody] List<FieldMappingUpsertRequest> mappings,
            IDataFeedService dataFeedService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.CurrentTenantId.HasValue)
            {
                return Results.Forbid();
            }

            if (mappings is null || mappings.Count == 0)
            {
                return Results.BadRequest(new { error = "At least one mapping is required." });
            }

            foreach (var mapping in mappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.ExternalFieldName) || string.IsNullOrWhiteSpace(mapping.TemplateFieldName))
                {
                    return Results.BadRequest(new { error = "ExternalFieldName and TemplateFieldName are required." });
                }

                await dataFeedService.UpsertFieldMapping(
                    tenantContext.CurrentTenantId.Value,
                    integrationName,
                    returnCode,
                    mapping.ExternalFieldName,
                    mapping.TemplateFieldName,
                    ct);
            }

            return Results.Ok(new { updated = mappings.Count });
        })
        .RequireAuthorization("CanCreateSubmission")
        .WithSummary("Upsert tenant field mappings for an integration/return.");
    }
}

public class FieldMappingUpsertRequest
{
    public string ExternalFieldName { get; set; } = string.Empty;
    public string TemplateFieldName { get; set; } = string.Empty;
}
