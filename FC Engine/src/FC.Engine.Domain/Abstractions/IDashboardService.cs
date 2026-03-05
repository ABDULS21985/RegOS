using FC.Engine.Domain.Models;

namespace FC.Engine.Domain.Abstractions;

public interface IDashboardService
{
    Task<DashboardSummary> GetSummary(Guid tenantId, CancellationToken ct = default);
    Task<ModuleDashboardData> GetModuleDashboard(Guid tenantId, string moduleCode, CancellationToken ct = default);
    Task<ComplianceSummaryData> GetComplianceSummary(Guid tenantId, CancellationToken ct = default);
    Task<TrendData> GetSubmissionTrend(Guid tenantId, string moduleCode, int periods = 6, CancellationToken ct = default);
    Task<TrendData> GetValidationErrorTrend(Guid tenantId, string moduleCode, int periods = 6, CancellationToken ct = default);
    Task<AdminDashboardData> GetAdminDashboard(Guid tenantId, CancellationToken ct = default);
    Task<PlatformDashboardData> GetPlatformDashboard(CancellationToken ct = default);
}

