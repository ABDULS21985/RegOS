using System.Security.Claims;

namespace FC.Engine.Api.Endpoints;

internal static class ApiClaimResolvers
{
    public static int GetUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? principal.FindFirstValue("user_id");

        return int.TryParse(raw, out var id) ? id : 0;
    }

    public static int GetInstitutionId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue("iid")
            ?? principal.FindFirstValue("institution_id")
            ?? principal.FindFirstValue("institutionId")
            ?? principal.FindFirstValue("InstitutionId");

        return int.TryParse(raw, out var id) ? id : 0;
    }

    public static int GetRegulatorId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue("RegulatorId")
            ?? principal.FindFirstValue("regulator_id");

        return int.TryParse(raw, out var id) ? id : 0;
    }

    public static string? GetRegulatorCode(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue("RegulatorCode");
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    public static bool IsPlatformAdmin(ClaimsPrincipal principal)
    {
        return principal.IsInRole("PlatformAdmin")
            || principal.HasClaim("IsPlatformAdmin", "true")
            || principal.HasClaim("perm", "admin.platform");
    }
}
