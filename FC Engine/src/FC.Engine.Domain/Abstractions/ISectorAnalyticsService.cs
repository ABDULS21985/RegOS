using FC.Engine.Domain.Models;

namespace FC.Engine.Domain.Abstractions;

public interface ISectorAnalyticsService
{
    Task<SectorCarDistribution> GetCarDistribution(string regulatorCode, string periodCode, CancellationToken ct = default);
    Task<SectorNplTrend> GetNplTrend(string regulatorCode, int quarters = 8, CancellationToken ct = default);
    Task<SectorDepositStructure> GetDepositStructure(string regulatorCode, string periodCode, CancellationToken ct = default);
    Task<FilingTimeliness> GetFilingTimeliness(string regulatorCode, string periodCode, CancellationToken ct = default);
    Task<FilingHeatmap> GetFilingHeatmap(string regulatorCode, string periodCode, CancellationToken ct = default);
}
