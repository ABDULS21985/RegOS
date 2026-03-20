namespace FC.Engine.Domain.Abstractions;

public interface IDataFeedService
{
    Task<DataFeedResult?> GetByIdempotencyKey(Guid tenantId, string idempotencyKey, CancellationToken ct = default);

    Task<DataFeedResult> ProcessFeed(
        Guid tenantId,
        string returnCode,
        DataFeedRequest request,
        string? idempotencyKey,
        CancellationToken ct = default);

    Task UpsertFieldMapping(
        Guid tenantId,
        string integrationName,
        string returnCode,
        string externalFieldName,
        string templateFieldName,
        CancellationToken ct = default);

    Task<IReadOnlyList<TenantFieldMappingEntry>> GetFieldMappings(
        Guid tenantId,
        string integrationName,
        string returnCode,
        CancellationToken ct = default);
}

public class DataFeedRequest
{
    public string PeriodCode { get; set; } = string.Empty; // yyyy-MM or yyyy-Qn
    public string? InstitutionCode { get; set; }
    public string? IntegrationName { get; set; }
    public List<DataFeedFieldValue> Fields { get; set; } = [];
}

public class DataFeedFieldValue
{
    public string FieldCode { get; set; } = string.Empty;
    public object? Value { get; set; }
}

public class DataFeedResult
{
    public bool Success { get; set; }
    public string ReturnCode { get; set; } = string.Empty;
    public int SubmissionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? IdempotencyKey { get; set; }
    public int RowsPersisted { get; set; }
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
    public List<string> Errors { get; set; } = [];
}

public class TenantFieldMappingEntry
{
    public string IntegrationName { get; set; } = string.Empty;
    public string ReturnCode { get; set; } = string.Empty;
    public string ExternalFieldName { get; set; } = string.Empty;
    public string TemplateFieldName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
