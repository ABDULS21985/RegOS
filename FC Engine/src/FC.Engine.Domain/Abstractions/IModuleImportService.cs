namespace FC.Engine.Domain.Abstractions;

public interface IModuleImportService
{
    Task<ModuleValidationResult> ValidateDefinition(string jsonDefinition, CancellationToken ct = default);
    Task<ModuleImportResult> ImportModule(string jsonDefinition, string performedBy, CancellationToken ct = default);
    Task<ModulePublishResult> PublishModule(string moduleCode, string approvedBy, CancellationToken ct = default);
}

public class ModuleValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public int TemplateCount { get; set; }
    public int FieldCount { get; set; }
    public int FormulaCount { get; set; }
    public int CrossSheetRuleCount { get; set; }
}

public class ModuleImportResult
{
    public bool Success { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public int TemplatesCreated { get; set; }
    public int FieldsCreated { get; set; }
    public int FormulasCreated { get; set; }
    public int CrossSheetRulesCreated { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ModulePublishResult
{
    public bool Success { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public int TablesCreated { get; set; }
    public int VersionsPublished { get; set; }
    public List<string> DdlStatements { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
