using FC.Engine.Domain.Abstractions;
using Microsoft.AspNetCore.Http;

namespace FC.Engine.Infrastructure.MultiTenancy;

internal readonly record struct TenantSessionContextScope(
    Guid? TenantId,
    bool BypassRls,
    string? TenantType,
    string? RegulatorCode);

internal static class TenantSessionContextScopeResolver
{
    public static TenantSessionContextScope Resolve(
        Guid? tenantId,
        IHttpContextAccessor httpContextAccessor)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var tenantType = ResolveSessionValue(httpContext?.Items["TenantType"], httpContext?.User.FindFirst("TenantType")?.Value);
        var regulatorCode = ResolveSessionValue(httpContext?.Items["RegulatorCode"], httpContext?.User.FindFirst("RegulatorCode")?.Value);

        if (tenantId.HasValue)
        {
            return new TenantSessionContextScope(tenantId.Value, false, tenantType, regulatorCode);
        }

        if (httpContext is null)
        {
            return new TenantSessionContextScope(null, true, tenantType, regulatorCode);
        }

        if (httpContext.User?.Identity?.IsAuthenticated != true)
        {
            return new TenantSessionContextScope(null, true, tenantType, regulatorCode);
        }

        var isPlatformAdmin = httpContext.User.IsInRole("PlatformAdmin")
            || httpContext.User.HasClaim("IsPlatformAdmin", "true");

        return isPlatformAdmin
            ? new TenantSessionContextScope(null, true, tenantType, regulatorCode)
            : new TenantSessionContextScope(Guid.Empty, false, tenantType, regulatorCode);
    }

    public static TenantSessionContextScope Resolve(
        ITenantContext tenantContext,
        IHttpContextAccessor httpContextAccessor)
    {
        return Resolve(tenantContext.CurrentTenantId, httpContextAccessor);
    }

    private static string? ResolveSessionValue(object? itemValue, string? claimValue)
    {
        if (itemValue is string value && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return string.IsNullOrWhiteSpace(claimValue) ? null : claimValue;
    }
}
