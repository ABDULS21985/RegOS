using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;

namespace FC.Engine.Portal.Services;

public sealed class PortalSubmissionLaunchService
{
    private static readonly IReadOnlyDictionary<string, int> WorkspacePriority =
        PortalModuleWorkspaceCatalog.All
            .Select((definition, index) => new KeyValuePair<string, int>(definition.ModuleCode, index))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

    private readonly ITenantContext _tenantContext;
    private readonly IEntitlementService _entitlementService;
    private readonly ITemplateMetadataCache _templateCache;

    public PortalSubmissionLaunchService(
        ITenantContext tenantContext,
        IEntitlementService entitlementService,
        ITemplateMetadataCache templateCache)
    {
        _tenantContext = tenantContext;
        _entitlementService = entitlementService;
        _templateCache = templateCache;
    }

    public async Task<PortalSubmissionLaunchTarget> ResolvePrimarySubmitAsync(Guid? tenantId = null, CancellationToken ct = default)
    {
        var effectiveTenantId = tenantId ?? _tenantContext.CurrentTenantId;
        if (effectiveTenantId is not { } currentTenantId)
        {
            return PortalSubmissionLaunchTarget.Fallback();
        }

        var entitlement = await _entitlementService.ResolveEntitlements(currentTenantId, ct);
        var activeModules = entitlement.ActiveModules
            .Where(x => x.IsActive && !string.IsNullOrWhiteSpace(x.ModuleCode))
            .GroupBy(x => x.ModuleCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToDictionary(x => x.ModuleCode, x => x, StringComparer.OrdinalIgnoreCase);

        if (activeModules.Count == 0)
        {
            return PortalSubmissionLaunchTarget.Fallback();
        }

        var templates = await _templateCache.GetAllPublishedTemplates(currentTenantId, ct);
        var candidate = templates
            .Where(template =>
                !string.IsNullOrWhiteSpace(template.ReturnCode) &&
                !string.IsNullOrWhiteSpace(template.ModuleCode) &&
                activeModules.ContainsKey(template.ModuleCode))
            .OrderBy(template => GetModulePriority(template.ModuleCode))
            .ThenBy(template => template.ModuleCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(template => template.ReturnCode, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (candidate is not null)
        {
            var moduleCode = candidate.ModuleCode!;
            var module = activeModules[moduleCode];
            return new PortalSubmissionLaunchTarget
            {
                Href = PortalSubmissionLinkBuilder.BuildSubmitHref(candidate.ReturnCode, moduleCode),
                ReturnCode = candidate.ReturnCode,
                ModuleCode = moduleCode,
                ModuleName = module.ModuleName,
                WorkspaceHref = PortalSubmissionLinkBuilder.ResolveWorkspaceHref(moduleCode)
            };
        }

        var fallbackModule = activeModules.Values
            .OrderBy(module => GetModulePriority(module.ModuleCode))
            .ThenBy(module => module.ModuleCode, StringComparer.OrdinalIgnoreCase)
            .First();

        return new PortalSubmissionLaunchTarget
        {
            Href = PortalSubmissionLinkBuilder.BuildSubmitHref(returnCode: null, fallbackModule.ModuleCode),
            ModuleCode = fallbackModule.ModuleCode,
            ModuleName = fallbackModule.ModuleName,
            WorkspaceHref = PortalSubmissionLinkBuilder.ResolveWorkspaceHref(fallbackModule.ModuleCode)
        };
    }

    private static int GetModulePriority(string? moduleCode) =>
        moduleCode is not null && WorkspacePriority.TryGetValue(moduleCode, out var priority)
            ? priority
            : int.MaxValue;
}

public sealed class PortalSubmissionLaunchTarget
{
    public string Href { get; init; } = "/submit";
    public string? ReturnCode { get; init; }
    public string? ModuleCode { get; init; }
    public string? ModuleName { get; init; }
    public string? WorkspaceHref { get; init; }

    public static PortalSubmissionLaunchTarget Fallback() => new();
}
