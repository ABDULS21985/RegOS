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

    public PartnerPortalService(
        IPartnerManagementService partnerService,
        IDashboardService dashboardService)
    {
        _partnerService = partnerService;
        _dashboardService = dashboardService;
    }

    public Task<bool> IsPartnerTenant(Guid tenantId, CancellationToken ct = default)
        => _partnerService.IsPartnerTenant(tenantId, ct);

    public Task<PartnerDashboardData> GetDashboard(Guid partnerTenantId, CancellationToken ct = default)
        => _dashboardService.GetPartnerDashboard(partnerTenantId, ct);

    public Task<List<PartnerSubTenantSummary>> GetSubTenants(Guid partnerTenantId, CancellationToken ct = default)
        => _partnerService.GetSubTenants(partnerTenantId, ct);

    public Task<List<PartnerSubTenantUserSummary>> GetSubTenantUsers(Guid partnerTenantId, Guid subTenantId, CancellationToken ct = default)
        => _partnerService.GetSubTenantUsers(partnerTenantId, subTenantId, ct);

    public Task<List<PartnerSubTenantSubmissionSummary>> GetSubTenantSubmissions(Guid partnerTenantId, Guid subTenantId, int take = 20, CancellationToken ct = default)
        => _partnerService.GetSubTenantSubmissions(partnerTenantId, subTenantId, take, ct);

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
}
