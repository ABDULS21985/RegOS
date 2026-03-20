using FC.Engine.Infrastructure.Services;

namespace FC.Engine.Admin.Services;

public sealed class PlatformIntelligenceRefreshService
{
    private readonly IPlatformIntelligenceWorkspaceLoader _workspaceLoader;
    private readonly DashboardBriefingPackBuilder _dashboardBriefingPackBuilder;
    private readonly PlatformIntelligenceRefreshRunStoreService _refreshRunStore;

    public PlatformIntelligenceRefreshService(
        IPlatformIntelligenceWorkspaceLoader workspaceLoader,
        DashboardBriefingPackBuilder dashboardBriefingPackBuilder,
        PlatformIntelligenceRefreshRunStoreService refreshRunStore)
    {
        _workspaceLoader = workspaceLoader;
        _dashboardBriefingPackBuilder = dashboardBriefingPackBuilder;
        _refreshRunStore = refreshRunStore;
    }

    public async Task<PlatformIntelligenceRefreshResult> RefreshAsync(CancellationToken ct = default)
    {
        var startedAtUtc = DateTime.UtcNow;

        try
        {
            var workspace = await _workspaceLoader.GetWorkspaceAsync(ct);
            var screeningSessionState = await _workspaceLoader.GetSanctionsScreeningSessionStateAsync(ct);
            var workflowState = await _workspaceLoader.GetSanctionsWorkflowStateAsync(ct);
            var strDraftCatalogState = await _workspaceLoader.GetSanctionsStrDraftCatalogStateAsync(ct);

            var dashboardPacksMaterialized = await MaterializeDashboardBriefingPacksAsync(
                workspace,
                screeningSessionState,
                workflowState,
                strDraftCatalogState,
                ct);

            var completedAtUtc = DateTime.UtcNow;
            var result = new PlatformIntelligenceRefreshResult
            {
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = completedAtUtc,
                DurationMilliseconds = (int)Math.Max(0, Math.Round((completedAtUtc - startedAtUtc).TotalMilliseconds)),
                GeneratedAt = workspace.GeneratedAt,
                InstitutionCount = workspace.InstitutionScorecards.Count,
                InterventionCount = workspace.Interventions.Count,
                TimelineCount = workspace.ActivityTimeline.Count,
                DashboardPacksMaterialized = dashboardPacksMaterialized,
                RolloutCatalogMaterializedAt = workspace.Rollout.CatalogMaterializedAt,
                KnowledgeCatalogMaterializedAt = workspace.KnowledgeGraph.CatalogMaterializedAt,
                KnowledgeDossierMaterializedAt = workspace.KnowledgeGraph.DossierMaterializedAt,
                CapitalPackMaterializedAt = workspace.Capital.ReturnPackMaterializedAt,
                SanctionsPackMaterializedAt = workspace.Sanctions.ReturnPackMaterializedAt,
                SanctionsStrDraftCatalogMaterializedAt = workspace.Sanctions.StrDraftCatalogMaterializedAt,
                ResiliencePackMaterializedAt = workspace.Resilience.ReturnPackMaterializedAt,
                ModelRiskPackMaterializedAt = workspace.ModelRisk.ReturnPackMaterializedAt
            };

            await _refreshRunStore.RecordSuccessAsync(
                new PlatformIntelligenceRefreshRunSuccessCommand
                {
                    StartedAtUtc = result.StartedAtUtc,
                    CompletedAtUtc = result.CompletedAtUtc,
                    GeneratedAtUtc = result.GeneratedAt,
                    DurationMilliseconds = result.DurationMilliseconds,
                    InstitutionCount = result.InstitutionCount,
                    InterventionCount = result.InterventionCount,
                    TimelineCount = result.TimelineCount,
                    DashboardPacksMaterialized = result.DashboardPacksMaterialized,
                    RolloutCatalogMaterializedAt = result.RolloutCatalogMaterializedAt,
                    KnowledgeCatalogMaterializedAt = result.KnowledgeCatalogMaterializedAt,
                    KnowledgeDossierMaterializedAt = result.KnowledgeDossierMaterializedAt,
                    CapitalPackMaterializedAt = result.CapitalPackMaterializedAt,
                    SanctionsPackMaterializedAt = result.SanctionsPackMaterializedAt,
                    SanctionsStrDraftCatalogMaterializedAt = result.SanctionsStrDraftCatalogMaterializedAt,
                    ResiliencePackMaterializedAt = result.ResiliencePackMaterializedAt,
                    ModelRiskPackMaterializedAt = result.ModelRiskPackMaterializedAt
                },
                ct);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var completedAtUtc = DateTime.UtcNow;

            try
            {
                await _refreshRunStore.RecordFailureAsync(
                    new PlatformIntelligenceRefreshRunFailureCommand
                    {
                        StartedAtUtc = startedAtUtc,
                        CompletedAtUtc = completedAtUtc,
                        DurationMilliseconds = (int)Math.Max(0, Math.Round((completedAtUtc - startedAtUtc).TotalMilliseconds)),
                        FailureMessage = ex.Message
                    },
                    ct);
            }
            catch
            {
            }

            throw;
        }
    }

    private async Task<int> MaterializeDashboardBriefingPacksAsync(
        PlatformIntelligenceWorkspace workspace,
        SanctionsScreeningSessionState screeningSessionState,
        SanctionsWorkflowState workflowState,
        SanctionsStrDraftCatalogState strDraftCatalogState,
        CancellationToken ct)
    {
        var materialized = 0;

        foreach (var lens in new[] { "governor", "deputy", "director" })
        {
            var sections = _dashboardBriefingPackBuilder.Build(
                workspace,
                lens,
                institutionId: null,
                screeningSessionState,
                workflowState,
                strDraftCatalogState);

            await _workspaceLoader.MaterializeDashboardBriefingPackAsync(lens, null, sections, ct);
            materialized++;
        }

        foreach (var institution in workspace.InstitutionDetails)
        {
            var sections = _dashboardBriefingPackBuilder.Build(
                workspace,
                "executive",
                institution.InstitutionId,
                screeningSessionState,
                workflowState,
                strDraftCatalogState);

            await _workspaceLoader.MaterializeDashboardBriefingPackAsync("executive", institution.InstitutionId, sections, ct);
            materialized++;
        }

        return materialized;
    }
}

public sealed class PlatformIntelligenceRefreshResult
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public int DurationMilliseconds { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int InstitutionCount { get; set; }
    public int InterventionCount { get; set; }
    public int TimelineCount { get; set; }
    public int DashboardPacksMaterialized { get; set; }
    public DateTime? RolloutCatalogMaterializedAt { get; set; }
    public DateTime? KnowledgeCatalogMaterializedAt { get; set; }
    public DateTime? KnowledgeDossierMaterializedAt { get; set; }
    public DateTime? CapitalPackMaterializedAt { get; set; }
    public DateTime? SanctionsPackMaterializedAt { get; set; }
    public DateTime? SanctionsStrDraftCatalogMaterializedAt { get; set; }
    public DateTime? ResiliencePackMaterializedAt { get; set; }
    public DateTime? ModelRiskPackMaterializedAt { get; set; }
}
