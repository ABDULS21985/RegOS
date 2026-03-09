using FC.Engine.Domain.Enums;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FC.Engine.Infrastructure.Services;

internal sealed class RegulatorTenantResolver : IRegulatorTenantResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly MetadataDbContext _db;
    private readonly IConfiguration _configuration;

    public RegulatorTenantResolver(
        IHttpContextAccessor httpContextAccessor,
        MetadataDbContext db,
        IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
        _configuration = configuration;
    }

    public async Task<RegulatorTenantContext> ResolveAsync(string regulatorCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(regulatorCode))
        {
            throw new ArgumentException("Regulator code is required.", nameof(regulatorCode));
        }

        regulatorCode = regulatorCode.Trim().ToUpperInvariant();

        var httpContext = _httpContextAccessor.HttpContext;
        var claimTenantId = httpContext?.User.FindFirst("TenantId")?.Value;
        if (Guid.TryParse(claimTenantId, out var tenantId))
        {
            return new RegulatorTenantContext(tenantId, regulatorCode);
        }

        var configuredTenantId = _configuration[$"ConductRiskSurveillance:RegulatorTenants:{regulatorCode}"];
        if (Guid.TryParse(configuredTenantId, out tenantId))
        {
            return new RegulatorTenantContext(tenantId, regulatorCode);
        }

        var scheduledRegulator = _configuration["SurveillanceCycle:RegulatorCode"];
        var scheduledTenantId = _configuration["SurveillanceCycle:RegulatorTenantId"];
        if (string.Equals(scheduledRegulator, regulatorCode, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(scheduledTenantId, out tenantId))
        {
            return new RegulatorTenantContext(tenantId, regulatorCode);
        }

        var candidates = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.TenantType == TenantType.Regulator)
            .Where(t => t.TenantSlug == regulatorCode.ToLower()
                     || t.TenantName == regulatorCode
                     || t.TenantName.Contains(regulatorCode))
            .Select(t => new { t.TenantId, t.TenantSlug, t.TenantName })
            .ToListAsync(ct);

        if (candidates.Count == 1)
        {
            return new RegulatorTenantContext(candidates[0].TenantId, regulatorCode);
        }

        var exactSlug = candidates.FirstOrDefault(x =>
            string.Equals(x.TenantSlug, regulatorCode.ToLower(), StringComparison.OrdinalIgnoreCase));

        if (exactSlug is not null)
        {
            return new RegulatorTenantContext(exactSlug.TenantId, regulatorCode);
        }

        throw new InvalidOperationException(
            $"Unable to resolve a regulator tenant for '{regulatorCode}'. Set ConductRiskSurveillance:RegulatorTenants:{regulatorCode} or run from a regulator-authenticated session.");
    }
}
