using FC.Engine.Domain.Models;

namespace FC.Engine.Domain.Abstractions;

public interface IPrivacyDashboardService
{
    Task<DpoDashboardData> GetDashboard(Guid? tenantId, CancellationToken ct = default);
}
