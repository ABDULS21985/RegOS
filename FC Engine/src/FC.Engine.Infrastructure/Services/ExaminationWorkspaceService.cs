using System.Text.Json;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Models;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FC.Engine.Infrastructure.Services;

public class ExaminationWorkspaceService : IExaminationWorkspaceService
{
    private readonly MetadataDbContext _db;
    private readonly IEntityBenchmarkingService _entityBenchmarking;
    private readonly ITenantBrandingService _brandingService;

    public ExaminationWorkspaceService(
        MetadataDbContext db,
        IEntityBenchmarkingService entityBenchmarking,
        ITenantBrandingService brandingService)
    {
        _db = db;
        _entityBenchmarking = entityBenchmarking;
        _brandingService = brandingService;
    }

    public async Task<IReadOnlyList<ExaminationProject>> ListProjects(Guid regulatorTenantId, CancellationToken ct = default)
    {
        return await _db.ExaminationProjects
            .AsNoTracking()
            .Where(x => x.TenantId == regulatorTenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<ExaminationProject> CreateProject(
        Guid regulatorTenantId,
        int createdBy,
        ExaminationProjectCreateRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Project name is required.", nameof(request));
        }

        var entityIds = request.InstitutionIds
            .Where(x => x > 0)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var moduleCodes = request.ModuleCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var now = DateTime.UtcNow;
        var project = new ExaminationProject
        {
            TenantId = regulatorTenantId,
            Name = request.Name.Trim(),
            Scope = string.IsNullOrWhiteSpace(request.Scope) ? "General examination scope" : request.Scope.Trim(),
            EntityIdsJson = JsonSerializer.Serialize(entityIds),
            ModuleCodesJson = JsonSerializer.Serialize(moduleCodes),
            PeriodFrom = request.PeriodFrom,
            PeriodTo = request.PeriodTo,
            Status = ExaminationProjectStatus.InProgress,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.ExaminationProjects.Add(project);
        await _db.SaveChangesAsync(ct);
        return project;
    }

    public async Task<ExaminationWorkspaceData?> GetWorkspace(
        Guid regulatorTenantId,
        string regulatorCode,
        int projectId,
        CancellationToken ct = default)
    {
        var project = await _db.ExaminationProjects
            .Include(x => x.Annotations)
            .FirstOrDefaultAsync(x => x.TenantId == regulatorTenantId && x.Id == projectId, ct);

        if (project is null)
        {
            return null;
        }

        var institutionIds = ParseIntList(project.EntityIdsJson);
        var moduleCodes = ParseStringList(project.ModuleCodesJson);

        var query = _db.Submissions
            .AsNoTracking()
            .Include(s => s.Institution)
            .Include(s => s.ReturnPeriod)
                .ThenInclude(rp => rp!.Module)
            .Where(s => s.ReturnPeriod != null
                        && s.ReturnPeriod.Module != null
                        && s.ReturnPeriod.Module.RegulatorCode == regulatorCode);

        if (institutionIds.Count > 0)
        {
            query = query.Where(s => institutionIds.Contains(s.InstitutionId));
        }

        if (moduleCodes.Count > 0)
        {
            query = query.Where(s => s.ReturnPeriod != null
                                     && s.ReturnPeriod.Module != null
                                     && moduleCodes.Contains(s.ReturnPeriod.Module.ModuleCode));
        }

        if (project.PeriodFrom.HasValue)
        {
            var from = project.PeriodFrom.Value;
            query = query.Where(s => s.SubmittedAt >= from);
        }

        if (project.PeriodTo.HasValue)
        {
            var to = project.PeriodTo.Value;
            query = query.Where(s => s.SubmittedAt <= to);
        }

        var rows = await query
            .OrderByDescending(s => s.SubmittedAt)
            .Take(2000)
            .ToListAsync(ct);

        var submissions = rows.Select(s => new RegulatorSubmissionInboxItem
        {
            SubmissionId = s.Id,
            TenantId = s.TenantId,
            InstitutionId = s.InstitutionId,
            InstitutionName = s.Institution?.InstitutionName ?? "Unknown",
            LicenceType = s.Institution?.LicenseType ?? "N/A",
            ModuleCode = s.ReturnPeriod?.Module?.ModuleCode ?? "N/A",
            ModuleName = s.ReturnPeriod?.Module?.ModuleName ?? "Unknown",
            PeriodLabel = s.ReturnPeriod is null ? "N/A" : RegulatorAnalyticsSupport.FormatPeriodLabel(s.ReturnPeriod),
            SubmittedAt = s.SubmittedAt,
            SubmissionStatus = s.Status.ToString(),
            ReceiptStatus = RegulatorReceiptStatus.Received,
            OpenQueryCount = 0
        }).ToList();

        var benchmarkMap = new Dictionary<int, EntityBenchmarkResult>();
        foreach (var institutionId in submissions.Select(x => x.InstitutionId).Distinct())
        {
            var benchmark = await _entityBenchmarking.GetEntityBenchmark(regulatorCode, institutionId, ct: ct);
            if (benchmark is not null)
            {
                benchmarkMap[institutionId] = benchmark;
            }
        }

        return new ExaminationWorkspaceData
        {
            Project = project,
            Submissions = submissions,
            Annotations = project.Annotations.OrderByDescending(x => x.CreatedAt).ToList(),
            BenchmarksByInstitution = benchmarkMap
        };
    }

    public async Task<ExaminationAnnotation> AddAnnotation(
        Guid regulatorTenantId,
        int projectId,
        int submissionId,
        int? institutionId,
        string? fieldCode,
        string note,
        int createdBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("Annotation note is required.", nameof(note));
        }

        var projectExists = await _db.ExaminationProjects
            .AnyAsync(x => x.TenantId == regulatorTenantId && x.Id == projectId, ct);
        if (!projectExists)
        {
            throw new InvalidOperationException($"Examination project {projectId} was not found.");
        }

        var annotation = new ExaminationAnnotation
        {
            TenantId = regulatorTenantId,
            ProjectId = projectId,
            SubmissionId = submissionId,
            InstitutionId = institutionId,
            FieldCode = string.IsNullOrWhiteSpace(fieldCode) ? null : fieldCode.Trim(),
            Note = note.Trim(),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _db.ExaminationAnnotations.Add(annotation);

        var project = await _db.ExaminationProjects.FirstAsync(x => x.Id == projectId, ct);
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return annotation;
    }

    public async Task<byte[]> GenerateReportPdf(
        Guid regulatorTenantId,
        string regulatorCode,
        int projectId,
        CancellationToken ct = default)
    {
        var workspace = await GetWorkspace(regulatorTenantId, regulatorCode, projectId, ct)
            ?? throw new InvalidOperationException($"Examination project {projectId} was not found.");

        QuestPDF.Settings.License = LicenseType.Community;

        var branding = BrandingConfig.WithDefaults(await _brandingService.GetBrandingConfig(regulatorTenantId, ct));
        var primaryColor = string.IsNullOrWhiteSpace(branding.PrimaryColor) ? "#0F766E" : branding.PrimaryColor!;

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("Regulator Examination Report").FontSize(16).Bold().FontColor(primaryColor);
                    col.Item().Text(workspace.Project.Name).FontSize(12).SemiBold();
                    col.Item().Text($"Generated: {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Text("Scope").Bold().FontColor(primaryColor);
                    col.Item().Text(workspace.Project.Scope);

                    col.Item().Text("Submission Review Summary").Bold().FontColor(primaryColor);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(primaryColor).Padding(3).Text("Institution").FontColor(Colors.White).Bold();
                            header.Cell().Background(primaryColor).Padding(3).Text("Module").FontColor(Colors.White).Bold();
                            header.Cell().Background(primaryColor).Padding(3).Text("Period").FontColor(Colors.White).Bold();
                            header.Cell().Background(primaryColor).Padding(3).Text("Status").FontColor(Colors.White).Bold();
                        });

                        foreach (var row in workspace.Submissions.Take(100))
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(row.InstitutionName);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(row.ModuleCode);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(row.PeriodLabel);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(row.SubmissionStatus);
                        }
                    });

                    col.Item().Text("Annotations").Bold().FontColor(primaryColor);
                    if (workspace.Annotations.Count == 0)
                    {
                        col.Item().Text("No annotations recorded.");
                    }
                    else
                    {
                        foreach (var annotation in workspace.Annotations.Take(150))
                        {
                            col.Item().PaddingBottom(2).Text($"- Submission #{annotation.SubmissionId}"
                                + (string.IsNullOrWhiteSpace(annotation.FieldCode) ? string.Empty : $" [{annotation.FieldCode}]")
                                + $": {annotation.Note}");
                        }
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text(branding.CopyrightText ?? "RegOS")
                        .FontSize(7)
                        .FontColor(Colors.Grey.Medium);

                    row.ConstantItem(100).AlignRight().Text(txt =>
                    {
                        txt.Span("Page ").FontSize(7);
                        txt.CurrentPageNumber().FontSize(7);
                        txt.Span(" / ").FontSize(7);
                        txt.TotalPages().FontSize(7);
                    });
                });
            });
        }).GeneratePdf();

        var project = await _db.ExaminationProjects.FirstAsync(x => x.Id == projectId, ct);
        project.LastReportGeneratedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return pdf;
    }

    private static List<int> ParseIntList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<int>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
        }
        catch
        {
            return new List<int>();
        }
    }

    private static List<string> ParseStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return (JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }
}
