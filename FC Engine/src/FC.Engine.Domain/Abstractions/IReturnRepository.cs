using FC.Engine.Domain.Returns;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Domain.Abstractions;

public interface IReturnRepository
{
    Task Save(IReturnData data, int submissionId, CancellationToken ct = default);
    Task<IReturnData?> GetBySubmissionPeriod(
        int institutionId, int returnPeriodId, ReturnCode returnCode, CancellationToken ct = default);
    Task<IReturnData?> GetBySubmissionId(int submissionId, CancellationToken ct = default);
}
