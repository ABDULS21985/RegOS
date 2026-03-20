using System.Security.Claims;

namespace FC.Engine.Portal.Services;

public static class PortalTenantClaimResolver
{
    private static readonly string[] TenantClaimTypes = ["TenantId", "tenant_id", "tid"];

    public static Guid ResolveTenantId(ClaimsPrincipal principal)
    {
        if (TryResolveTenantId(principal, out var tenantId))
        {
            return tenantId;
        }

        throw new InvalidOperationException("Tenant context is missing from the current session.");
    }

    public static bool TryResolveTenantId(ClaimsPrincipal principal, out Guid tenantId)
    {
        tenantId = Guid.Empty;

        foreach (var claimType in TenantClaimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (Guid.TryParse(value, out tenantId))
            {
                return true;
            }
        }

        return false;
    }
}
