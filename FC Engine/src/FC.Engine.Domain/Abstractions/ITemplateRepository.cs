using FC.Engine.Domain.Metadata;

namespace FC.Engine.Domain.Abstractions;

public interface ITemplateRepository
{
    Task<ReturnTemplate?> GetById(int id, CancellationToken ct = default);
    Task<ReturnTemplate?> GetByReturnCode(string returnCode, CancellationToken ct = default);
    Task<ReturnTemplate?> GetPublishedByReturnCode(string returnCode, CancellationToken ct = default);
    Task<IReadOnlyList<ReturnTemplate>> GetAll(CancellationToken ct = default);
    Task<IReadOnlyList<ReturnTemplate>> GetByFrequency(string frequency, CancellationToken ct = default);
    Task Add(ReturnTemplate template, CancellationToken ct = default);
    Task Update(ReturnTemplate template, CancellationToken ct = default);
    Task<bool> ExistsByReturnCode(string returnCode, CancellationToken ct = default);
}
