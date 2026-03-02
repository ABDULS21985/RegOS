using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Returns;

namespace FC.Engine.Domain.Abstractions;

public interface IValidationEngine
{
    Task<ValidationReport> Validate(IReturnData data, Submission submission, CancellationToken ct = default);
}
