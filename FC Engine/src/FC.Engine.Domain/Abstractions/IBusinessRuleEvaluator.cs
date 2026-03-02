using FC.Engine.Domain.DataRecord;
using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Abstractions;

public interface IBusinessRuleEvaluator
{
    Task<IReadOnlyList<ValidationError>> Evaluate(
        ReturnDataRecord record,
        Submission submission,
        CancellationToken ct = default);
}
