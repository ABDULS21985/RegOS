using FC.Engine.Domain.Models;

namespace FC.Engine.Domain.Abstractions;

public interface IEarlyWarningService
{
    Task<List<EarlyWarningFlag>> ComputeFlags(string regulatorCode, CancellationToken ct = default);
}
