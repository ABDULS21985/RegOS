using FC.Engine.Application.DTOs;
using FC.Engine.Application.Services;
using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Api.Endpoints;

public static class SubmissionEndpoints
{
    public static void MapSubmissionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/submissions").WithTags("Submissions");

        // POST /api/submissions/{returnCode}
        group.MapPost("/{returnCode}", async (
            string returnCode,
            [FromHeader(Name = "X-Institution-Code")] string institutionCode,
            [FromHeader(Name = "X-Reporting-Year")] int reportingYear,
            [FromHeader(Name = "X-Reporting-Month")] int reportingMonth,
            HttpRequest request,
            IngestionOrchestrator orchestrator,
            FcEngineDbContext db,
            CancellationToken ct) =>
        {
            // Resolve institution
            var institution = await db.Institutions
                .FirstOrDefaultAsync(i => i.InstitutionCode == institutionCode, ct);
            if (institution == null)
                return Results.BadRequest(new { error = $"Institution '{institutionCode}' not found" });

            // Resolve return period
            var period = await db.ReturnPeriods
                .FirstOrDefaultAsync(p => p.Year == reportingYear && p.Month == reportingMonth, ct);
            if (period == null)
                return Results.BadRequest(new { error = $"Return period {reportingYear}-{reportingMonth:D2} not found" });

            var result = await orchestrator.ProcessSubmission(
                request.Body, returnCode, institution.Id, period.Id, ct);

            return result.Status switch
            {
                "Accepted" or "AcceptedWithWarnings" => Results.Ok(result),
                "Rejected" => Results.UnprocessableEntity(result),
                _ => Results.StatusCode(500)
            };
        })
        .Accepts<IFormFile>("application/xml")
        .Produces<SubmissionResultDto>(200)
        .Produces<SubmissionResultDto>(422)
        .WithName("SubmitReturn")
        .WithOpenApi();

        // GET /api/submissions/{id}
        group.MapGet("/{id:int}", async (
            int id,
            IngestionOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var result = await orchestrator.GetSubmission(id, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .Produces<SubmissionDto>(200)
        .WithName("GetSubmission")
        .WithOpenApi();

        // GET /api/submissions/{id}/validation-report
        group.MapGet("/{id:int}/validation-report", async (
            int id,
            IngestionOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var report = await orchestrator.GetValidationReport(id, ct);
            return report is not null ? Results.Ok(report) : Results.NotFound();
        })
        .Produces<ValidationReportDto>(200)
        .WithName("GetValidationReport")
        .WithOpenApi();
    }
}
