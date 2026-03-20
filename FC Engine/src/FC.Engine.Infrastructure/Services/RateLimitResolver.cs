using System.Collections.Concurrent;
using FC.Engine.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.Services;

public class RateLimitResolver : IRateLimitResolver
{
    private readonly ConcurrentDictionary<string, (string PlanCode, DateTime ExpiresAt)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RateLimitResolver> _logger;

    public RateLimitResolver(IServiceProvider serviceProvider, ILogger<RateLimitResolver> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public string GetTenantTier(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return "DEFAULT";

        if (_cache.TryGetValue(tenantId, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
            return entry.PlanCode;

        return "DEFAULT";
    }

    public async Task WarmAsync(Guid tenantId, CancellationToken ct = default)
    {
        var key = tenantId.ToString();

        // Skip if already cached and not expired
        if (_cache.TryGetValue(key, out var existing) && existing.ExpiresAt > DateTime.UtcNow)
            return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var entitlementService = scope.ServiceProvider.GetRequiredService<IEntitlementService>();
            var entitlement = await entitlementService.ResolveEntitlements(tenantId, ct);

            var planCode = string.IsNullOrWhiteSpace(entitlement.PlanCode) ? "DEFAULT" : entitlement.PlanCode;
            _cache[key] = (planCode, DateTime.UtcNow.Add(CacheTtl));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to warm rate-limit cache for tenant {TenantId}", tenantId);
        }
    }
}
