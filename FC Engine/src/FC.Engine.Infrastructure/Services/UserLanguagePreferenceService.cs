using System.Security.Claims;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public class UserLanguagePreferenceService : IUserLanguagePreferenceService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly MetadataDbContext _db;

    public UserLanguagePreferenceService(
        IHttpContextAccessor httpContextAccessor,
        MetadataDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public async Task<string> GetCurrentLanguage(CancellationToken ct = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        if (user is not null && user.Identity?.IsAuthenticated == true)
        {
            var claimLang = user.FindFirst("lang")?.Value
                            ?? user.FindFirst("PreferredLanguage")?.Value;
            if (!string.IsNullOrWhiteSpace(claimLang))
            {
                return NormalizeLanguage(claimLang);
            }

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                var persisted = await _db.InstitutionUsers
                    .AsNoTracking()
                    .Where(x => x.Id == userId)
                    .Select(x => x.PreferredLanguage)
                    .FirstOrDefaultAsync(ct);
                if (!string.IsNullOrWhiteSpace(persisted))
                {
                    return NormalizeLanguage(persisted);
                }
            }
        }

        var acceptLanguage = httpContext?.Request.Headers["Accept-Language"].ToString();
        if (!string.IsNullOrWhiteSpace(acceptLanguage))
        {
            var first = acceptLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
            {
                return NormalizeLanguage(first);
            }
        }

        return "en";
    }

    private static string NormalizeLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "en";
        }

        var trimmed = languageCode.Trim().ToLowerInvariant();
        var dash = trimmed.IndexOf('-');
        return dash > 0 ? trimmed[..dash] : trimmed;
    }
}
