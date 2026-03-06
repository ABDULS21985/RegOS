using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Admin.Services;

public class TenantManagementService
{
    private readonly MetadataDbContext _db;
    private readonly ITenantOnboardingService _onboardingService;
    private readonly IAuditLogger _auditLogger;
    private readonly ITenantContext _tenantContext;

    public TenantManagementService(
        MetadataDbContext db,
        ITenantOnboardingService onboardingService,
        IAuditLogger auditLogger,
        ITenantContext tenantContext)
    {
        _db = db;
        _onboardingService = onboardingService;
        _auditLogger = auditLogger;
        _tenantContext = tenantContext;
    }

    public async Task<List<Tenant>> GetAllTenantsAsync(CancellationToken ct = default)
    {
        return await _db.Tenants
            .OrderBy(t => t.TenantName)
            .ToListAsync(ct);
    }

    public async Task<Tenant?> GetTenantByIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.Tenants.FindAsync(new object[] { tenantId }, ct);
    }

    public async Task<TenantDashboardStats> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var tenants = await _db.Tenants.ToListAsync(ct);
        return new TenantDashboardStats
        {
            TotalTenants = tenants.Count,
            ActiveTenants = tenants.Count(t => t.Status == TenantStatus.Active),
            PendingTenants = tenants.Count(t => t.Status == TenantStatus.PendingActivation),
            SuspendedTenants = tenants.Count(t => t.Status == TenantStatus.Suspended)
        };
    }

    public async Task<TenantOnboardingResult> OnboardTenantAsync(TenantOnboardingRequest request, CancellationToken ct = default)
    {
        return await _onboardingService.OnboardTenant(request, ct);
    }

    public async Task ActivateTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, ct)
            ?? throw new InvalidOperationException("Tenant not found");
        tenant.Activate();
        await _db.SaveChangesAsync(ct);
        await LogPlatformAction("TenantActivated", tenantId, ct);
    }

    public async Task SuspendTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, ct)
            ?? throw new InvalidOperationException("Tenant not found");
        tenant.Suspend("Admin action");
        await _db.SaveChangesAsync(ct);
        await LogPlatformAction("TenantSuspended", tenantId, ct);
    }

    public async Task ReactivateTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, ct)
            ?? throw new InvalidOperationException("Tenant not found");
        tenant.Reactivate();
        await _db.SaveChangesAsync(ct);
        await LogPlatformAction("TenantReactivated", tenantId, ct);
    }

    public async Task DeactivateTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, ct)
            ?? throw new InvalidOperationException("Tenant not found");
        tenant.Deactivate();
        await _db.SaveChangesAsync(ct);
        await LogPlatformAction("TenantDeactivated", tenantId, ct);
    }

    public async Task<List<TenantLicenceType>> GetTenantLicencesAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.TenantLicenceTypes
            .Include(tlt => tlt.LicenceType)
            .Where(tlt => tlt.TenantId == tenantId)
            .ToListAsync(ct);
    }

    public async Task<List<LicenceType>> GetAllLicenceTypesAsync(CancellationToken ct = default)
    {
        return await _db.LicenceTypes
            .Where(lt => lt.IsActive)
            .OrderBy(lt => lt.DisplayOrder)
            .ToListAsync(ct);
    }

    public async Task<List<Module>> GetAllModulesAsync(CancellationToken ct = default)
    {
        return await _db.Modules
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync(ct);
    }

    public async Task<string?> GetTenantName(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.Tenants
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .Select(t => t.TenantName)
            .FirstOrDefaultAsync(ct);
    }

    private async Task LogPlatformAction(string action, Guid tenantId, CancellationToken ct)
    {
        await _auditLogger.Log(
            "Tenant",
            0,
            action,
            null,
            new
            {
                IsPlatformAdmin = _tenantContext.IsPlatformAdmin,
                ImpersonatedTenantId = _tenantContext.ImpersonatingTenantId,
                TenantId = tenantId
            },
            "platform-admin",
            ct);
    }
}

public class TenantDashboardStats
{
    public int TotalTenants { get; set; }
    public int ActiveTenants { get; set; }
    public int PendingTenants { get; set; }
    public int SuspendedTenants { get; set; }
}
