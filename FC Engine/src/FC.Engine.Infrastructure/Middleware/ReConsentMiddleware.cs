using System.Security.Claims;
using FC.Engine.Domain.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FC.Engine.Infrastructure.Middleware;

public class ReConsentMiddleware
{
    private static readonly string[] BypassPrefixes =
    [
        "/privacy/reconsent",
        "/account/reconsent",
        "/account/logout",
        "/account/login",
        "/login",
        "/_framework",
        "/_blazor",
        "/css",
        "/js",
        "/images",
        "/favicon",
        "/hubs",
        "/swagger",
        "/health",
        "/metrics"
    ];

    private readonly RequestDelegate _next;

    public ReConsentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        IConsentService consentService)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (ShouldBypass(path))
        {
            await _next(context);
            return;
        }

        if (context.User.IsInRole("PlatformAdmin") || context.User.HasClaim("IsPlatformAdmin", "true"))
        {
            await _next(context);
            return;
        }

        var tenantId = tenantContext.CurrentTenantId;
        var userIdRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!tenantId.HasValue || !int.TryParse(userIdRaw, out var userId))
        {
            await _next(context);
            return;
        }

        var userType = context.User.HasClaim(c => c.Type == "InstitutionId")
            ? "InstitutionUser"
            : "PortalUser";
        var hasConsent = await consentService.HasCurrentRequiredConsent(
            tenantId.Value,
            userId,
            userType,
            context.RequestAborted);

        if (hasConsent)
        {
            await _next(context);
            return;
        }

        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status428PreconditionRequired;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Re-consent required",
                policyVersion = consentService.GetCurrentPolicyVersion()
            });
            return;
        }

        var returnUrl = Uri.EscapeDataString($"{context.Request.Path}{context.Request.QueryString}");
        context.Response.Redirect($"/privacy/reconsent?returnUrl={returnUrl}");
    }

    private static bool ShouldBypass(string path)
    {
        foreach (var prefix in BypassPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public static class ReConsentMiddlewareExtensions
{
    public static IApplicationBuilder UseReConsent(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ReConsentMiddleware>();
    }
}
