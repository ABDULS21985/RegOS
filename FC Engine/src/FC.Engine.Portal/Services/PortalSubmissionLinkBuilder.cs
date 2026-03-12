namespace FC.Engine.Portal.Services;

public static class PortalSubmissionLinkBuilder
{
    public static string BuildSubmitHref(string returnCode, string? moduleCode) =>
        string.IsNullOrWhiteSpace(moduleCode)
            ? $"/submit?returnCode={Uri.EscapeDataString(returnCode)}"
            : $"/submit?module={Uri.EscapeDataString(moduleCode)}&returnCode={Uri.EscapeDataString(returnCode)}";

    public static string? ResolveWorkspaceHref(string? moduleCode) =>
        PortalModuleWorkspaceCatalog.TryGetDefinition(moduleCode, out var definition)
            ? PortalModuleWorkspaceCatalog.GetWorkspaceHref(definition.ModuleCode)
            : null;

    public static string? ResolveModuleName(string? moduleCode) =>
        PortalModuleWorkspaceCatalog.TryGetDefinition(moduleCode, out var definition)
            ? definition.Title
            : moduleCode;
}
