namespace FC.Engine.Portal.Components.Shared;

/// <summary>
/// Represents a single segment in a portal breadcrumb trail.
/// </summary>
/// <param name="Label">Display text shown for this segment.</param>
/// <param name="Url">Navigation URL; null for the current (last) segment (renders as plain text).</param>
/// <param name="IsCurrent">True for the last/active segment — adds aria-current="page".</param>
/// <param name="ChipTooltip">When set, the segment renders as a highlighted chip with a CSS tooltip (e.g. period with deadline info).</param>
/// <param name="HasStatusDot">Appends a pulsing status dot after the label — used for active/open submission periods.</param>
public record BreadcrumbItem(
    string Label,
    string? Url = null,
    bool IsCurrent = false,
    string? ChipTooltip = null,
    bool HasStatusDot = false
);
