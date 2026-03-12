namespace FC.Engine.Portal.Services;

public static class PortalSubmissionLinkBuilder
{
    public static string BuildSubmitHref(string? returnCode, string? moduleCode, int? periodId = null)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(moduleCode))
        {
            query.Add($"module={Uri.EscapeDataString(moduleCode)}");
        }

        if (!string.IsNullOrWhiteSpace(returnCode))
        {
            query.Add($"returnCode={Uri.EscapeDataString(returnCode)}");
        }

        if (periodId is > 0)
        {
            query.Add($"periodId={periodId.Value}");
        }

        return query.Count == 0
            ? "/submit"
            : $"/submit?{string.Join("&", query)}";
    }

    public static string? ResolveWorkspaceHref(string? moduleCode) =>
        PortalModuleWorkspaceCatalog.TryGetDefinition(moduleCode, out var definition)
            ? PortalModuleWorkspaceCatalog.GetWorkspaceHref(definition.ModuleCode)
            : null;

    public static string? ResolveModuleName(string? moduleCode) =>
        PortalModuleWorkspaceCatalog.TryGetDefinition(moduleCode, out var definition)
            ? definition.Title
            : moduleCode;
}
