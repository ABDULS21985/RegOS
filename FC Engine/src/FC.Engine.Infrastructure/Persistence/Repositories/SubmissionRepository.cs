using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Persistence.Repositories;

public class SubmissionRepository : ISubmissionRepository
{
    private readonly FcEngineDbContext _db;

    public SubmissionRepository(FcEngineDbContext db)
    {
        _db = db;
    }

    public async Task<Submission?> GetById(int id, CancellationToken ct = default)
    {
        return await _db.Submissions
            .Include(s => s.Institution)
            .Include(s => s.ReturnPeriod)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Submission?> GetByIdWithReport(int id, CancellationToken ct = default)
    {
        return await _db.Submissions
            .Include(s => s.Institution)
            .Include(s => s.ReturnPeriod)
            .Include(s => s.ValidationReport)
                .ThenInclude(r => r!.Errors)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<int> Add(Submission submission, CancellationToken ct = default)
    {
        _db.Submissions.Add(submission);
        await _db.SaveChangesAsync(ct);
        return submission.Id;
    }

    public async Task Update(Submission submission, CancellationToken ct = default)
    {
        _db.Submissions.Update(submission);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Submission>> GetByInstitutionAndPeriod(
        int institutionId, int returnPeriodId, CancellationToken ct = default)
    {
        return await _db.Submissions
            .Where(s => s.InstitutionId == institutionId && s.ReturnPeriodId == returnPeriodId)
            .ToListAsync(ct);
    }
}
