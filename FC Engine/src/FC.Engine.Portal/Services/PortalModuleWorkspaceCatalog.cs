namespace FC.Engine.Portal.Services;

public static class PortalModuleWorkspaceCatalog
{
    private static readonly IReadOnlyDictionary<string, PortalModuleWorkspaceDefinition> Definitions =
        new Dictionary<string, PortalModuleWorkspaceDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["CAPITAL_SUPERVISION"] = new(
                ModuleCode: "CAPITAL_SUPERVISION",
                Slug: "capital-supervision",
                Title: "Capital Supervision",
                Eyebrow: "Capital Stack, Buffers, and RWA",
                Summary: "Coordinate capital planning, buffer monitoring, RWA optimisation, and filing readiness from one institution workspace.",
                FocusAreas:
                [
                    "Review capital buffers and planning scenarios before the filing window tightens.",
                    "Launch return preparation straight from the module queue instead of hunting across generic screens.",
                    "Keep recent filings, open periods, and operating guidance in one place."
                ]),
            ["OPS_RESILIENCE"] = new(
                ModuleCode: "OPS_RESILIENCE",
                Slug: "ops-resilience",
                Title: "Operational Resilience",
                Eyebrow: "Services, Testing, and Recovery",
                Summary: "Run important service inventories, impact tolerances, testing cycles, incident follow-up, and resilience submissions from a single workflow center.",
                FocusAreas:
                [
                    "Track open resilience periods, service inventories, and filing deadlines together.",
                    "Route teams from readiness checks directly into templates and submissions.",
                    "Keep incident, recovery, and board-ready evidence visible during the filing cycle."
                ]),
            ["MODEL_RISK"] = new(
                ModuleCode: "MODEL_RISK",
                Slug: "model-risk",
                Title: "Model Risk & Validation",
                Eyebrow: "Inventory, Validation, and Approval",
                Summary: "Manage model inventories, validation cadence, backtesting packs, approval workflow, and regulatory returns in one governed operating surface.",
                FocusAreas:
                [
                    "See approval workflow, validation cadence, and filing status without leaving the institution portal.",
                    "Start the right model-risk return directly from the module workspace.",
                    "Keep evidence, recent filings, and help guidance aligned with the active module."
                ])
        };

    private static readonly IReadOnlyDictionary<string, string> ModuleCodeByKey =
        Definitions.Values
            .SelectMany(definition => new[]
            {
                new KeyValuePair<string, string>(definition.ModuleCode, definition.ModuleCode),
                new KeyValuePair<string, string>(definition.Slug, definition.ModuleCode)
            })
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PortalModuleWorkspaceDefinition> All => Definitions.Values.ToList();

    public static bool HasWorkspace(string? moduleKey) => TryGetDefinition(moduleKey, out _);

    public static string? ResolveModuleCode(string? moduleKey)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            return null;
        }

        return ModuleCodeByKey.TryGetValue(moduleKey.Trim(), out var code)
            ? code
            : null;
    }

    public static bool TryGetDefinition(string? moduleKey, out PortalModuleWorkspaceDefinition definition)
    {
        definition = default!;

        var moduleCode = ResolveModuleCode(moduleKey);
        if (moduleCode is null)
        {
            return false;
        }

        return Definitions.TryGetValue(moduleCode, out definition!);
    }

    public static string GetWorkspaceHref(string moduleCode)
    {
        if (TryGetDefinition(moduleCode, out var definition))
        {
            return $"/workflows/{definition.Slug}";
        }

        var fallback = (moduleCode ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(fallback)
            ? "/modules"
            : $"/workflows/{Uri.EscapeDataString(fallback.ToLowerInvariant())}";
    }
}

public sealed record PortalModuleWorkspaceDefinition(
    string ModuleCode,
    string Slug,
    string Title,
    string Eyebrow,
    string Summary,
    IReadOnlyList<string> FocusAreas);
