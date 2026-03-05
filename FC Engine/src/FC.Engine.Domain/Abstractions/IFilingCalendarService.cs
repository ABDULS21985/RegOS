using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Models;

namespace FC.Engine.Domain.Abstractions;

public interface IFilingCalendarService
{
    /// <summary>Get RAG status items for all active modules for a tenant.</summary>
    Task<List<RagItem>> GetRagStatus(Guid tenantId, CancellationToken ct = default);

    /// <summary>Compute the deadline date for a module + period combination.</summary>
    DateTime ComputeDeadline(Module module, ReturnPeriod period);

    /// <summary>Override the deadline for a specific period (e.g., regulator extension).</summary>
    Task OverrideDeadline(Guid tenantId, int periodId, DateTime newDeadline, string reason, int overrideByUserId, CancellationToken ct = default);

    /// <summary>Record SLA tracking when a submission is made.</summary>
    Task RecordSla(int periodId, int submissionId, CancellationToken ct = default);
}
