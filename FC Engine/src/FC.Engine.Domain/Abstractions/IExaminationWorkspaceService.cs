using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Models;

namespace FC.Engine.Domain.Abstractions;

public interface IExaminationWorkspaceService
{
    Task<IReadOnlyList<ExaminationProject>> ListProjects(Guid regulatorTenantId, CancellationToken ct = default);

    Task<ExaminationProject> CreateProject(
        Guid regulatorTenantId,
        int createdBy,
        ExaminationProjectCreateRequest request,
        CancellationToken ct = default);

    Task<ExaminationWorkspaceData?> GetWorkspace(
        Guid regulatorTenantId,
        string regulatorCode,
        int projectId,
        CancellationToken ct = default);

    Task<ExaminationAnnotation> AddAnnotation(
        Guid regulatorTenantId,
        int projectId,
        int submissionId,
        int? institutionId,
        string? fieldCode,
        string note,
        int createdBy,
        CancellationToken ct = default);

    Task<byte[]> GenerateReportPdf(
        Guid regulatorTenantId,
        string regulatorCode,
        int projectId,
        CancellationToken ct = default);
}
