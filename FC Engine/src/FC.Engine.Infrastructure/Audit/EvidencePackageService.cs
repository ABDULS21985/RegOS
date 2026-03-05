using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Audit;

public class EvidencePackageService : IEvidencePackageService
{
    private readonly MetadataDbContext _db;
    private readonly IGenericDataRepository _dataRepo;
    private readonly IFileStorageService _storage;
    private readonly ITenantContext _tenantContext;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public EvidencePackageService(
        MetadataDbContext db,
        IGenericDataRepository dataRepo,
        IFileStorageService storage,
        ITenantContext tenantContext)
    {
        _db = db;
        _dataRepo = dataRepo;
        _storage = storage;
        _tenantContext = tenantContext;
    }

    public async Task<EvidencePackage> GenerateAsync(int submissionId, string generatedBy, CancellationToken ct = default)
    {
        var submission = await _db.Submissions
            .Include(s => s.Institution)
            .Include(s => s.ReturnPeriod)
            .Include(s => s.ValidationReport)
                .ThenInclude(v => v!.Errors)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct)
            ?? throw new InvalidOperationException($"Submission {submissionId} not found");

        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("Tenant context required for evidence package generation");

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 1. Data snapshot
            var dataRecord = await _dataRepo.GetBySubmission(submission.ReturnCode, submissionId, ct);
            var dataJson = JsonSerializer.Serialize(dataRecord, JsonOptions);
            await AddEntryAsync(archive, "data_snapshot.json", dataJson);

            // 2. Data hash
            var dataHash = ComputeSha256(dataJson);
            await AddEntryAsync(archive, "data_hash.sha256", dataHash);

            // 3. Approval chain
            var approvals = await _db.SubmissionApprovals
                .Where(a => a.SubmissionId == submissionId)
                .OrderBy(a => a.RequestedAt)
                .Select(a => new
                {
                    a.Id,
                    a.Status,
                    a.RequestedByUserId,
                    a.ReviewedByUserId,
                    a.SubmitterNotes,
                    a.ReviewerComments,
                    a.RequestedAt,
                    a.ReviewedAt
                })
                .ToListAsync(ct);
            await AddEntryAsync(archive, "approval_chain.json", JsonSerializer.Serialize(approvals, JsonOptions));

            // 4. Validation report
            var validationData = submission.ValidationReport != null
                ? new
                {
                    submission.ValidationReport.Id,
                    submission.ValidationReport.CreatedAt,
                    submission.ValidationReport.FinalizedAt,
                    submission.ValidationReport.IsValid,
                    submission.ValidationReport.ErrorCount,
                    submission.ValidationReport.WarningCount,
                    Errors = submission.ValidationReport.Errors.Select(e => new
                    {
                        e.RuleId,
                        e.Field,
                        e.Message,
                        Severity = e.Severity.ToString(),
                        Category = e.Category.ToString(),
                        e.ExpectedValue,
                        e.ActualValue
                    })
                }
                : (object?)null;
            await AddEntryAsync(archive, "validation_report.json", JsonSerializer.Serialize(validationData, JsonOptions));

            // 5. Audit trail
            var auditEntries = await _db.AuditLog
                .Where(a => a.EntityType == "Submission" && a.EntityId == submissionId)
                .OrderBy(a => a.SequenceNumber)
                .Select(a => new
                {
                    a.SequenceNumber,
                    a.Action,
                    a.PerformedBy,
                    a.PerformedAt,
                    a.OldValues,
                    a.NewValues,
                    a.Hash,
                    a.PreviousHash
                })
                .ToListAsync(ct);
            await AddEntryAsync(archive, "audit_trail.json", JsonSerializer.Serialize(auditEntries, JsonOptions));

            // 6. Attachment manifest (export requests serve as attachments)
            var exports = await _db.ExportRequests
                .Where(e => e.SubmissionId == submissionId)
                .OrderBy(e => e.RequestedAt)
                .Select(e => new
                {
                    e.Id,
                    Format = e.Format.ToString(),
                    e.FilePath,
                    e.FileSize,
                    e.Sha256Hash,
                    e.RequestedAt,
                    e.CompletedAt
                })
                .ToListAsync(ct);
            await AddEntryAsync(archive, "attachment_manifest.json", JsonSerializer.Serialize(exports, JsonOptions));

            // 7. Submission metadata
            var metadata = new
            {
                submission.Id,
                submission.ReturnCode,
                Status = submission.Status.ToString(),
                submission.InstitutionId,
                InstitutionName = submission.Institution?.InstitutionName,
                submission.ReturnPeriodId,
                PeriodLabel = submission.ReturnPeriod != null
                    ? $"{submission.ReturnPeriod.Year}-{submission.ReturnPeriod.Month:D2}"
                    : null,
                submission.SubmittedAt,
                submission.CreatedAt,
                submission.SubmittedByUserId,
                submission.ApprovalRequired,
                submission.ProcessingDurationMs,
                GeneratedAt = DateTime.UtcNow,
                GeneratedBy = generatedBy
            };
            await AddEntryAsync(archive, "submission_metadata.json", JsonSerializer.Serialize(metadata, JsonOptions));
        }

        // Compute hash of the ZIP
        zipStream.Position = 0;
        var packageHash = ComputeSha256(zipStream);
        var fileSize = zipStream.Length;

        // Upload immutably
        zipStream.Position = 0;
        var storagePath = $"evidence/{tenantId}/{submissionId}/{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
        await _storage.UploadImmutableAsync(storagePath, zipStream, "application/zip", ct);

        // Save record
        var package = new EvidencePackage
        {
            TenantId = tenantId,
            SubmissionId = submissionId,
            PackageHash = packageHash,
            StoragePath = storagePath,
            FileSizeBytes = fileSize,
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = generatedBy
        };

        _db.EvidencePackages.Add(package);
        await _db.SaveChangesAsync(ct);

        return package;
    }

    private static async Task AddEntryAsync(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        await writer.WriteAsync(content);
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(bytes);
    }

    private static string ComputeSha256(Stream stream)
    {
        var bytes = SHA256.HashData(stream);
        return Convert.ToHexStringLower(bytes);
    }
}
