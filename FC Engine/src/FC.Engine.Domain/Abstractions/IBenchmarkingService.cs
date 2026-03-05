using FC.Engine.Domain.Models;

namespace FC.Engine.Domain.Abstractions;

public interface IBenchmarkingService
{
    Task<BenchmarkResult?> GetPeerBenchmark(Guid tenantId, string moduleCode, CancellationToken ct = default);
}

