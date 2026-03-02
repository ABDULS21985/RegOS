using FC.Engine.Domain.DataRecord;

namespace FC.Engine.Domain.Abstractions;

/// <summary>
/// Generic data repository that uses Dapper to read/write to any physical data table
/// based on template metadata. Replaces all 103 per-template EF entity classes.
/// </summary>
public interface IGenericDataRepository
{
    Task Save(ReturnDataRecord record, int submissionId, CancellationToken ct = default);
    Task<ReturnDataRecord?> GetBySubmission(string returnCode, int submissionId, CancellationToken ct = default);
    Task<ReturnDataRecord?> GetByInstitutionAndPeriod(string returnCode, int institutionId, int returnPeriodId, CancellationToken ct = default);
    Task DeleteBySubmission(string returnCode, int submissionId, CancellationToken ct = default);
}
