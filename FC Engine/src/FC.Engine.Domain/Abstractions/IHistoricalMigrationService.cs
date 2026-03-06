using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Abstractions;

public interface IHistoricalMigrationService
{
    Task<IReadOnlyList<ImportJobDto>> GetJobs(
        Guid tenantId,
        int? institutionId = null,
        CancellationToken ct = default);

    Task<ImportJobDto> UploadAndParse(
        Guid tenantId,
        int institutionId,
        string returnCode,
        int returnPeriodId,
        string fileName,
        Stream fileStream,
        int importedBy,
        CancellationToken ct = default);

    Task<ImportJobDto?> GetJob(Guid tenantId, int importJobId, CancellationToken ct = default);
    Task<ImportMappingEditorDto> GetMappingEditor(Guid tenantId, int importJobId, CancellationToken ct = default);

    Task SaveMapping(
        Guid tenantId,
        int importJobId,
        IReadOnlyList<ImportMappingUpdate> updates,
        string? sourceIdentifier,
        CancellationToken ct = default);

    Task<ImportJobDto> ValidateJob(Guid tenantId, int importJobId, CancellationToken ct = default);
    Task<ImportJobDto> StageJob(Guid tenantId, int importJobId, CancellationToken ct = default);
    Task<ImportJobDto> CommitJob(Guid tenantId, int importJobId, CancellationToken ct = default);
    Task<ImportStagedReviewDto> GetStagedReview(Guid tenantId, int importJobId, int take = 200, CancellationToken ct = default);
    Task SaveStagedReview(
        Guid tenantId,
        int importJobId,
        IReadOnlyList<ImportStagedRecordDto> records,
        CancellationToken ct = default);

    Task<MigrationTrackerDto> GetTracker(Guid tenantId, CancellationToken ct = default);
    Task SetModuleSignOff(
        Guid tenantId,
        int moduleId,
        bool signedOff,
        int signedOffByUserId,
        string? notes,
        CancellationToken ct = default);
}

public class ImportJobDto
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int TemplateId { get; set; }
    public string ReturnCode { get; set; } = string.Empty;
    public int InstitutionId { get; set; }
    public int? ReturnPeriodId { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public HistoricalSourceFormat SourceFormat { get; set; }
    public ImportJobStatus Status { get; set; }
    public int RecordCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int ImportedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ImportMappingEditorDto
{
    public int ImportJobId { get; set; }
    public string ReturnCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public List<ImportColumnMapping> Mappings { get; set; } = [];
    public List<ImportFieldOption> AvailableFields { get; set; } = [];
    public List<string> UnmappedColumns { get; set; } = [];
    public List<string> UnmappedFields { get; set; } = [];
}

public class ImportFieldOption
{
    public string FieldName { get; set; } = string.Empty;
    public string FieldLabel { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
}

public class ImportMappingUpdate
{
    public int SourceIndex { get; set; }
    public string SourceHeader { get; set; } = string.Empty;
    public string? TargetFieldName { get; set; }
    public bool Ignored { get; set; }
}

public class MigrationTrackerDto
{
    public int TotalModules { get; set; }
    public int ModulesMigrated { get; set; }
    public List<MigrationModuleProgressDto> Modules { get; set; } = [];
}

public class MigrationModuleProgressDto
{
    public int ModuleId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public int ImportedPeriods { get; set; }
    public int TotalPeriods { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public bool AutoSignOffEligible { get; set; }
    public bool SignOff { get; set; }
    public int? SignedOffBy { get; set; }
    public DateTime? SignedOffAt { get; set; }
    public string? SignOffNotes { get; set; }
}

public class ImportStagedReviewDto
{
    public int ImportJobId { get; set; }
    public string ReturnCode { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = [];
    public List<ImportStagedRecordDto> Records { get; set; } = [];
    public int TotalRecords { get; set; }
}

public class ImportStagedRecordDto
{
    public int RowNumber { get; set; }
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
