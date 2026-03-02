using FC.Engine.Domain.DataRecord;
using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Abstractions;

public interface IFormulaEvaluator
{
    Task<IReadOnlyList<ValidationError>> Evaluate(ReturnDataRecord record, CancellationToken ct = default);
}
