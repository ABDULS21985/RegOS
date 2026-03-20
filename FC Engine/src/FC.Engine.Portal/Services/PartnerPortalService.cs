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
}
