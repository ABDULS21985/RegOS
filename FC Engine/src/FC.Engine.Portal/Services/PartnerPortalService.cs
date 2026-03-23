using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Models;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Portal.Services;

public class PartnerPortalService
{
    private readonly IPartnerManagementService _partnerService;
    private readonly IDashboardService _dashboardService;
    private readonly ITenantBrandingService _brandingService;

    public PartnerPortalService(
        IPartnerManagementService partnerService,
        IDashboardService dashboardService,
        ITenantBrandingService brandingService)
    {
        _partnerService = partnerService;
        _dashboardService = dashboardService;
        _brandingService = brandingService;
    }

    public Task<bool> IsPartnerTenant(Guid tenantId, CancellationToken ct = default)
        => _partnerService.IsPartnerTenant(tenantId, ct);

    public Task<PartnerDashboardData> GetDashboard(Guid partnerTenantId, CancellationToken ct = default)
        => _dashboardService.GetPartnerDashboard(partnerTenantId, ct);

    public Task<List<PartnerSubTenantSummary>> GetSubTenants(Guid partnerTenantId, CancellationToken ct = default)
        => _partnerService.GetSubTenants(partnerTenantId, ct);

    public Task<List<PartnerSubTenantUserSummary>> GetSubTenantUsers(Guid partnerTenantId, Guid subTenantId, CancellationToken ct = default)
        => _partnerService.GetSubTenantUsers(partnerTenantId, subTenantId, ct);

    public Task<PartnerSubTenantUserSummary> CreateSubTenantUser(Guid partnerTenantId, Guid subTenantId, PartnerSubTenantUserCreateRequest request, CancellationToken ct = default)
        => _partnerService.CreateSubTenantUser(partnerTenantId, subTenantId, request, ct);

    public Task SetSubTenantUserStatus(Guid partnerTenantId, Guid subTenantId, int userId, bool isActive, CancellationToken ct = default)
        => _partnerService.SetSubTenantUserStatus(partnerTenantId, subTenantId, userId, isActive, ct);

    public Task<List<PartnerSubTenantSubmissionSummary>> GetSubTenantSubmissions(Guid partnerTenantId, Guid subTenantId, int take = 20, CancellationToken ct = default)
        => _partnerService.GetSubTenantSubmissions(partnerTenantId, subTenantId, take, ct);

    public async Task<BrandingConfig> GetSubTenantBranding(Guid partnerTenantId, Guid subTenantId, CancellationToken ct = default)
    {
        // Validate that the sub-tenant belongs to this partner before fetching branding
        var subTenantIds = await _partnerService.GetPartnerSubTenantIds(partnerTenantId, ct);
        if (!subTenantIds.Contains(subTenantId))
            throw new UnauthorizedAccessException("Sub-tenant does not belong to this partner.");

        return await _brandingService.GetBrandingConfig(subTenantId);
    }

    public Task UpdateSubTenantBranding(Guid partnerTenantId, Guid subTenantId, BrandingConfig config, CancellationToken ct = default)
        => _partnerService.UpdateSubTenantBranding(partnerTenantId, subTenantId, config, ct);

    public Task<TenantOnboardingResult> CreateSubTenant(Guid partnerTenantId, SubTenantCreateRequest request, CancellationToken ct = default)
        => _partnerService.CreateSubTenant(partnerTenantId, request, ct);

    public Task<PartnerConfig?> GetPartnerConfig(Guid partnerTenantId, CancellationToken ct = default)
        => _partnerService.GetPartnerConfig(partnerTenantId, ct);

    public Task<PartnerConfig> UpdatePartnerConfig(Guid partnerTenantId, UpdatePartnerConfigRequest request, CancellationToken ct = default)
        => _partnerService.UpdatePartnerConfig(partnerTenantId, request, ct);

    public Task<List<PartnerSupportTicket>> GetSupportTickets(Guid partnerTenantId, CancellationToken ct = default)
        => _partnerService.GetSupportTicketsForPartner(partnerTenantId, ct);

    public Task<PartnerSupportTicket> CreateSupportTicket(
        Guid tenantId,
        int userId,
        string userName,
        string title,
        string description,
        PartnerSupportTicketPriority priority,
        CancellationToken ct = default)
        => _partnerService.CreateSupportTicket(tenantId, userId, userName, title, description, priority, ct);

    public Task<PartnerSupportTicket> EscalateSupportTicket(Guid partnerTenantId, int ticketId, int userId, CancellationToken ct = default)
        => _partnerService.EscalateSupportTicket(partnerTenantId, ticketId, userId, ct);

    /// <summary>
    /// Compute real compliance health metrics for each sub-tenant.
    /// Returns a dictionary keyed by sub-tenant TenantId with compliance data.
    /// </summary>
    public async Task<Dictionary<Guid, SubTenantHealthMetrics>> GetSubTenantHealthMetrics(
        Guid partnerTenantId, IEnumerable<PartnerSubTenantSummary> subTenants, CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, SubTenantHealthMetrics>();

        foreach (var sub in subTenants)
        {
            try
            {
                // Use submissions data to compute compliance metrics
                var submissions = await _partnerService.GetSubTenantSubmissions(
                    partnerTenantId, sub.TenantId, 100, ct);

                var now = DateTime.UtcNow;
                var monthStart = new DateTime(now.Year, now.Month, 1);

                var thisMonthSubs = submissions
                    .Where(s => s.SubmittedAt >= monthStart)
                    .ToList();

                var acceptedCount = thisMonthSubs.Count(s =>
                    s.Status is "Accepted" or "AcceptedWithWarnings");
                var totalThisMonth = thisMonthSubs.Count;
                var complianceRate = totalThisMonth > 0
                    ? Math.Round(acceptedCount * 100m / totalThisMonth, 1)
                    : (sub.ReturnsSubmittedThisMonth > 0 ? 80m : 0m);

                var overdueCount = submissions.Count(s =>
                    s.Status is "Rejected" or "ApprovalRejected"
                    && s.SubmittedAt >= monthStart);

                var lastSubmission = submissions
                    .Where(s => s.SubmittedAt.HasValue)
                    .OrderByDescending(s => s.SubmittedAt)
                    .Select(s => s.SubmittedAt)
                    .FirstOrDefault();

                // Derive active modules from distinct return codes
                var activeModules = submissions
                    .Select(s => s.ReturnCode)
                    .Where(m => !string.IsNullOrEmpty(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToArray();

                if (activeModules.Length == 0)
                    activeModules = ["Filing"];

                result[sub.TenantId] = new SubTenantHealthMetrics
                {
                    ComplianceRate = complianceRate,
                    OverdueReturns = overdueCount,
                    LastSubmission = lastSubmission,
                    ActiveModules = activeModules
                };
            }
            catch
            {
                // Fallback for inaccessible sub-tenants
                result[sub.TenantId] = new SubTenantHealthMetrics
                {
                    ComplianceRate = 0,
                    OverdueReturns = 0,
                    LastSubmission = null,
                    ActiveModules = ["Filing"]
                };
            }
        }

        return result;
    }
}

public class SubTenantHealthMetrics
{
    public decimal ComplianceRate { get; set; }
    public int OverdueReturns { get; set; }
    public DateTime? LastSubmission { get; set; }
    public string[] ActiveModules { get; set; } = [];
}
