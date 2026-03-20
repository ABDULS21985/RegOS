using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Abstractions;

public interface IPortalRoleCatalogService
{
    Task<IReadOnlyList<PortalRoleOption>> GetPortalRoles(Guid? tenantId, CancellationToken ct = default);
    Task EnsurePortalRoleConfigured(PortalRole role, Guid? tenantId, CancellationToken ct = default);
}

public sealed class PortalRoleOption
{
    public PortalRole Role { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    public bool IsTenantSpecific { get; set; }
}
