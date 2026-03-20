using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Metadata.Repositories;

public class SubmissionApprovalRepository : ISubmissionApprovalRepository
{
    private readonly IDbContextFactory<MetadataDbContext> _dbFactory;

    public SubmissionApprovalRepository(IDbContextFactory<MetadataDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<SubmissionApproval?> GetBySubmission(int submissionId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.SubmissionApprovals
            .Include(a => a.RequestedBy)
            .Include(a => a.ReviewedBy)
            .FirstOrDefaultAsync(a => a.SubmissionId == submissionId, ct);
    }

    public async Task<IReadOnlyList<SubmissionApproval>> GetPendingByInstitution(int institutionId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.SubmissionApprovals
            .Include(a => a.Submission)
            .Include(a => a.RequestedBy)
            .Where(a => a.Status == ApprovalStatus.Pending
                     && a.RequestedBy != null
                     && a.RequestedBy.InstitutionId == institutionId)
            .OrderByDescending(a => a.RequestedAt)
            .ToListAsync(ct);
    }

    public async Task Create(SubmissionApproval approval, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        db.SubmissionApprovals.Add(approval);
        await db.SaveChangesAsync(ct);
    }

    public async Task Update(SubmissionApproval approval, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        db.SubmissionApprovals.Update(approval);
        await db.SaveChangesAsync(ct);
    }

    public async Task Delete(SubmissionApproval approval, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        db.SubmissionApprovals.Remove(approval);
        await db.SaveChangesAsync(ct);
    }
}
