using FC.Engine.Domain.DataRecord;
using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Abstractions;

public interface ICrossSheetValidator
{
    Task<IReadOnlyList<ValidationError>> Validate(
        ReturnDataRecord currentRecord,
        int institutionId,
        int returnPeriodId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ValidationError>> ValidateCrossModule(
        Guid tenantId,
        int submissionId,
        string moduleCode,
        int institutionId,
        int returnPeriodId,
        CancellationToken ct = default);
}
