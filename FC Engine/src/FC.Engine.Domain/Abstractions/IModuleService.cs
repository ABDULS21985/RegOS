namespace FC.Engine.Domain.Abstractions;

public interface IModuleService
{
    Task<IReadOnlyList<ModuleSummaryDto>> GetModuleSummaries(CancellationToken ct = default);
    Task<ModuleDetailDto?> GetModuleDetail(string moduleCode, CancellationToken ct = default);
}

public sealed class ModuleSummaryDto
{
    public int Id { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string RegulatorCode { get; set; } = string.Empty;
    public int SheetCount { get; set; }
    public bool IsActive { get; set; }
    public string? CurrentVersion { get; set; }
    public int ActiveTenants { get; set; }
}

public sealed class ModuleDetailDto
{
    public int Id { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string RegulatorCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SheetCount { get; set; }
    public bool IsActive { get; set; }
    public List<ModuleVersionSummaryDto> Versions { get; set; } = new();
    public List<ModuleTemplateSummaryDto> Templates { get; set; } = new();
}

public sealed class ModuleVersionSummaryDto
{
    public string VersionCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public DateTime? DeprecatedAt { get; set; }
}

public sealed class ModuleTemplateSummaryDto
{
    public string ReturnCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PhysicalTableName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FieldCount { get; set; }
    public int FormulaCount { get; set; }
}
