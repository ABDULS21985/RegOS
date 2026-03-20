using FC.Engine.Admin.Services;
using FC.Engine.Infrastructure.Services;

namespace FC.Engine.Admin.Services.Sanctions;

/// <summary>
/// Wraps PlatformIntelligenceService screening methods and SanctionsScreeningSessionStoreService
/// to provide name and batch screening against the persisted watchlist catalog.
/// All data flows through real DB-backed services.
/// </summary>
public sealed class ScreeningEngineService
{
    private readonly PlatformIntelligenceService _intelligence;
    private readonly SanctionsScreeningSessionStoreService _sessionStore;
    private readonly SanctionsWatchlistCatalogService _watchlistCatalog;
    private readonly SanctionsWatchlistRefreshService _watchlistRefresh;

    public ScreeningEngineService(
        PlatformIntelligenceService intelligence,
        SanctionsScreeningSessionStoreService sessionStore,
        SanctionsWatchlistCatalogService watchlistCatalog,
        SanctionsWatchlistRefreshService watchlistRefresh)
    {
        _intelligence = intelligence;
        _sessionStore = sessionStore;
        _watchlistCatalog = watchlistCatalog;
        _watchlistRefresh = watchlistRefresh;
    }

    public async Task<SanctionsScreeningRun> ScreenNamesAsync(
        IEnumerable<string> names, double threshold, CancellationToken ct = default)
    {
        return await _intelligence.ScreenSubjectsAsync(names, threshold, ct);
    }

    public async Task<SanctionsTransactionScreeningResult> ScreenTransactionAsync(
        SanctionsTransactionScreeningRequest request, CancellationToken ct = default)
    {
        return await _intelligence.ScreenTransactionAsync(request, ct);
    }

    public async Task<SanctionsScreeningSessionState> GetLatestSessionAsync(CancellationToken ct = default)
    {
        return await _sessionStore.LoadLatestAsync(ct);
    }

    public async Task<ScreeningDashboardData> GetDashboardAsync(CancellationToken ct = default)
    {
        var ws = await _intelligence.GetWorkspaceAsync(ct);
        var sanctions = ws.Sanctions;
        var session = await _sessionStore.LoadLatestAsync(ct);

        return new ScreeningDashboardData
        {
            SourceCount = sanctions.SourceCount,
            EntryCount = sanctions.EntryCount,
            LastUpdatedAt = sanctions.LastUpdatedAt,
            FalsePositiveCount = sanctions.PersistedFalsePositiveCount,
            ReviewAuditCount = sanctions.PersistedReviewAuditCount,
            LastReviewedAt = sanctions.LastReviewedAt,
            TfsLinkedFieldCount = sanctions.TfsLinkedFieldCount,
            ReturnPackAttentionCount = sanctions.ReturnPackAttentionCount,
            ReturnPackMaterializedAt = sanctions.ReturnPackMaterializedAt,
            StrDraftCatalogMaterializedAt = sanctions.StrDraftCatalogMaterializedAt,
            Sources = sanctions.Sources,
            ReturnPack = sanctions.ReturnPack,
            LatestRun = session.LatestRun,
            LatestTransaction = session.LatestTransaction
        };
    }

    public async Task<SanctionsCatalogState> GetWatchlistCatalogAsync(CancellationToken ct = default)
    {
        return await _watchlistCatalog.LoadAsync(ct);
    }

    public async Task RefreshWatchlistAsync(CancellationToken ct = default)
    {
        await _watchlistRefresh.RefreshBaselineAsync(ct);
    }
}

public sealed class ScreeningDashboardData
{
    public int SourceCount { get; set; }
    public int EntryCount { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public int FalsePositiveCount { get; set; }
    public int ReviewAuditCount { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public int TfsLinkedFieldCount { get; set; }
    public int ReturnPackAttentionCount { get; set; }
    public DateTime? ReturnPackMaterializedAt { get; set; }
    public DateTime? StrDraftCatalogMaterializedAt { get; set; }
    public List<SanctionsWatchlistSource> Sources { get; set; } = [];
    public List<SanctionsPackSectionState> ReturnPack { get; set; } = [];
    public SanctionsStoredScreeningRun? LatestRun { get; set; }
    public SanctionsStoredTransactionCheck? LatestTransaction { get; set; }
}
