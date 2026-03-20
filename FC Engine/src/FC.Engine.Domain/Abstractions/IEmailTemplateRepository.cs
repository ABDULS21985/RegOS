using FC.Engine.Domain.Entities;

namespace FC.Engine.Domain.Abstractions;

public interface IEmailTemplateRepository
{
    Task<EmailTemplate?> GetActiveTemplate(string templateCode, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<EmailTemplate>> GetTemplatesForTenant(Guid tenantId, CancellationToken ct = default);
}
