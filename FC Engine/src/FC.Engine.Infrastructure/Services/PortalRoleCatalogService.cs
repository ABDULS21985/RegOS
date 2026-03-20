using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Security;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public class PortalRoleCatalogService : IPortalRoleCatalogService
{
    private readonly MetadataDbContext _db;
    private readonly IPermissionService _permissionService;

    public PortalRoleCatalogService(MetadataDbContext db, IPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    public async Task<IReadOnlyList<PortalRoleOption>> GetPortalRoles(Guid? tenantId, CancellationToken ct = default)
    {
        var portalRoleNames = Enum.GetNames<PortalRole>();

        var configuredRoles = await _db.Roles
            .AsNoTracking()
            .Where(r => r.IsActive
                && portalRoleNames.Contains(r.RoleName)
                && (r.TenantId == tenantId || r.TenantId == null))
            .OrderByDescending(r => r.TenantId.HasValue)
            .ThenBy(r => r.RoleName)
            .ToListAsync(ct);

        if (configuredRoles.Count == 0)
            return await BuildFallbackRoles(tenantId, ct);

        var selectedRoles = configuredRoles
            .GroupBy(r => r.RoleName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToDictionary(r => r.RoleName, StringComparer.OrdinalIgnoreCase);

        var options = new List<PortalRoleOption>();
        foreach (var role in OrderedPortalRoles())
        {
            if (!selectedRoles.TryGetValue(role.ToString(), out var configured))
                continue;

            options.Add(new PortalRoleOption
            {
                Role = role,
                DisplayName = configured.RoleName,
                Description = string.IsNullOrWhiteSpace(configured.Description)
                    ? GetDefaultDescription(role)
                    : configured.Description,
                Permissions = await _permissionService.GetPermissions(tenantId, configured.RoleName, ct),
                IsTenantSpecific = configured.TenantId.HasValue
            });
        }

        return options;
    }

    public async Task EnsurePortalRoleConfigured(PortalRole role, Guid? tenantId, CancellationToken ct = default)
    {
        if (!await _db.Roles.AsNoTracking().AnyAsync(ct))
            return;

        var exists = await _db.Roles
            .AsNoTracking()
            .AnyAsync(
                r => r.IsActive
                    && r.RoleName == role.ToString()
                    && (r.TenantId == tenantId || r.TenantId == null),
                ct);

        if (!exists)
            throw new InvalidOperationException($"Role '{role}' is not configured in the role catalog.");
    }

    private async Task<IReadOnlyList<PortalRoleOption>> BuildFallbackRoles(Guid? tenantId, CancellationToken ct)
    {
        var options = new List<PortalRoleOption>();
        foreach (var role in OrderedPortalRoles())
        {
            options.Add(new PortalRoleOption
            {
                Role = role,
                DisplayName = role.ToString(),
                Description = GetDefaultDescription(role),
                Permissions = await _permissionService.GetPermissions(tenantId, role.ToString(), ct),
                IsTenantSpecific = false
            });
        }

        return options;
    }

    private static IEnumerable<PortalRole> OrderedPortalRoles()
    {
        yield return PortalRole.Admin;
        yield return PortalRole.Approver;
        yield return PortalRole.Viewer;
    }

    private static string GetDefaultDescription(PortalRole role) => role switch
    {
        PortalRole.Admin => "Full platform administration access including user management, system configuration, and all data operations.",
        PortalRole.Approver => "Can review, approve, and manage template workflows, version publishing, and submission oversight.",
        PortalRole.Viewer => "Read-only access to permitted templates, submissions, and dashboard analytics.",
        _ => PermissionCatalog.DefaultRolePermissions.TryGetValue(role.ToString(), out var permissions)
            ? $"Assigned {permissions.Count} default permission(s)."
            : "Role configuration"
    };
}
