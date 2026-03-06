using FC.Engine.Domain.Models;

namespace FC.Engine.Domain.Abstractions;

public interface IEntityBenchmarkingService
{
    Task<EntityBenchmarkResult?> GetEntityBenchmark(
        string regulatorCode,
        int institutionId,
        string? periodCode = null,
        CancellationToken ct = default);
}
