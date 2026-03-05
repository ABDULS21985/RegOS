namespace FC.Engine.Domain.Abstractions;

public interface ITemplateDownloadService
{
    Task<byte[]> GenerateTemplateExcel(Guid tenantId, string returnCode, CancellationToken ct = default);
    Task<string> GenerateTemplateCsv(Guid tenantId, string returnCode, CancellationToken ct = default);
}
