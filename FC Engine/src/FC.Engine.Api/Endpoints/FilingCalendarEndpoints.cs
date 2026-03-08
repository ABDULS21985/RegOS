using System.Security.Claims;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Security;

namespace FC.Engine.Api.Endpoints;

public static class FilingCalendarEndpoints
{
    public static void MapFilingCalendarEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/filing-calendar").WithTags("Filing Calendar");

        group.MapGet("/rag", async (
            ITenantContext tenantContext,
            IFilingCalendarService filingCalendarService,
            CancellationToken ct) =>
        {
            if (!tenantContext.CurrentTenantId.HasValue)
            {
                return Results.Forbid();
            }

            var items = await filingCalendarService.GetRagStatus(tenantContext.CurrentTenantId.Value, ct);
            return Results.Ok(items);
        })
        .RequireAuthorization($"perm:{PermissionCatalog.CalendarRead}")
        .WithName("GetFilingCalendarRag")
        .WithSummary("Get filing RAG status for the current tenant");

        group.MapPost("/deadline-override", async (
            DeadlineOverrideRequest request,
            ClaimsPrincipal principal,
            ITenantContext tenantContext,
            IFilingCalendarService filingCalendarService,
            CancellationToken ct) =>
        {
            if (!tenantContext.CurrentTenantId.HasValue)
            {
                return Results.Forbid();
            }

            if (request.PeriodId <= 0)
            {
                return Results.BadRequest(new { error = "Invalid periodId." });
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { error = "Override reason is required." });
            }

            if (request.NewDeadline.Date < DateTime.UtcNow.Date)
            {
                return Results.BadRequest(new { error = "New deadline cannot be in the past." });
            }

            var overrideByUserId = ResolveUserId(principal);
            if (overrideByUserId is null)
            {
                return Results.Forbid();
            }

            await filingCalendarService.OverrideDeadline(
                tenantContext.CurrentTenantId.Value,
                request.PeriodId,
                request.NewDeadline.Date,
                request.Reason.Trim(),
                overrideByUserId.Value,
                ct);

            return Results.Ok(new
            {
                overridden = true,
                request.PeriodId,
                deadline = request.NewDeadline.Date
            });
        })
        .RequireAuthorization($"perm:{PermissionCatalog.CalendarManage}")
        .WithName("OverrideFilingDeadline")
        .WithSummary("Override a filing deadline for a tenant period");
    }

    private static int? ResolveUserId(ClaimsPrincipal principal)
    {
        var candidate = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? principal.FindFirstValue("sub");
        return int.TryParse(candidate, out var userId) ? userId : null;
    }
}

public sealed record DeadlineOverrideRequest(int PeriodId, DateTime NewDeadline, string Reason);
