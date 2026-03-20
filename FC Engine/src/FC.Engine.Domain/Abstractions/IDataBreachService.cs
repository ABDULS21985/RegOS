using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Models;

namespace FC.Engine.Domain.Abstractions;

public interface IDataBreachService
{
    Task<DataBreachIncident> ReportBreach(DataBreachReport report, CancellationToken ct = default);
    Task<DataBreachIncident> MarkNitdaNotified(int incidentId, int processedByUserId, string? notes, CancellationToken ct = default);
    Task<IReadOnlyList<DataBreachIncident>> GetOpenIncidents(Guid? tenantId, CancellationToken ct = default);
}
