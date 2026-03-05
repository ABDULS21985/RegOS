namespace FC.Engine.Domain.Abstractions;

/// <summary>
/// Resolves the subscription plan tier for a tenant, used by the rate-limiting middleware.
/// The method is synchronous because the rate-limiter partition factory does not support async.
/// Call <see cref="WarmAsync"/> after tenant resolution to populate the cache.
/// </summary>
public interface IRateLimitResolver
{
    /// <summary>
    /// Returns the cached plan code for the tenant (e.g. "STARTER", "ENTERPRISE").
    /// Returns "DEFAULT" if the tenant has not been warmed yet.
    /// </summary>
    string GetTenantTier(string tenantId);

    /// <summary>
    /// Asynchronously resolves and caches the plan code for the given tenant.
    /// Should be called once per request after tenant resolution.
    /// </summary>
    Task WarmAsync(Guid tenantId, CancellationToken ct = default);
}
