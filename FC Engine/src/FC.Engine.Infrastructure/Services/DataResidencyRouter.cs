using FC.Engine.Domain.Abstractions;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace FC.Engine.Infrastructure.Services;

public class DataResidencyRouter : IDataResidencyRouter
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly MetadataDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public DataResidencyRouter(
        MetadataDbContext db,
        IConfiguration configuration,
        IMemoryCache cache)
    {
        _db = db;
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<string> ResolveConnectionString(Guid? tenantId, CancellationToken ct = default)
    {
        var region = await ResolveRegion(tenantId, ct);
        var mappedName = _configuration[$"DataResidency:RegionConnectionStrings:{region}"];

        var connectionString = !string.IsNullOrWhiteSpace(mappedName)
            ? _configuration.GetConnectionString(mappedName)
            : null;

        return connectionString
               ?? _configuration.GetConnectionString("FcEngine")
               ?? throw new InvalidOperationException("Connection string 'FcEngine' not found");
    }

    public Task<string> ResolveRegion(Guid? tenantId, CancellationToken ct = default)
    {
        var cacheKey = $"data-residency:region:{tenantId?.ToString() ?? "global"}";
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = CacheTtl;

            if (!tenantId.HasValue)
            {
                return _configuration["DataResidency:DefaultRegion"] ?? "SouthAfricaNorth";
            }

            var jurisdictionIds = await _db.Institutions
                .AsNoTracking()
                .Where(i => i.TenantId == tenantId.Value)
                .Select(i => i.JurisdictionId)
                .Distinct()
                .ToListAsync(ct);

            if (jurisdictionIds.Count == 0)
            {
                return _configuration["DataResidency:DefaultRegion"] ?? "SouthAfricaNorth";
            }

            var regions = await _db.Jurisdictions
                .AsNoTracking()
                .Where(j => jurisdictionIds.Contains(j.Id))
                .Select(j => j.DataResidencyRegion)
                .Distinct()
                .ToListAsync(ct);

            if (regions.Count == 1)
            {
                return regions[0];
            }

            if (regions.Count > 1)
            {
                return _configuration["DataResidency:MultiJurisdictionRegion"]
                       ?? _configuration["DataResidency:DefaultRegion"]
                       ?? regions.OrderBy(r => r).First();
            }

            return _configuration["DataResidency:DefaultRegion"] ?? "SouthAfricaNorth";
        })!;
    }
}
