using FC.Engine.Admin.Services;

namespace FC.Engine.Admin.Services.Resilience;

/// <summary>
/// Aggregates operational resilience data from PlatformIntelligenceService workspace.
/// All data sourced from real DB entities (OpsResilience packs, assessments, incidents, etc.).
/// </summary>
public sealed class ResilienceOrchestratorService
{
    private readonly PlatformIntelligenceService _intelligence;

    public ResilienceOrchestratorService(PlatformIntelligenceService intelligence)
    {
        _intelligence = intelligence;
    }

    public async Task<ResilienceDashboardData> GetDashboardAsync(CancellationToken ct = default)
    {
        var ws = await _intelligence.GetWorkspaceAsync(ct);
        var r = ws.Resilience;
        return new ResilienceDashboardData
        {
            ImportantServiceCount = r.ImportantServiceCount,
            OpenIncidentCount = r.OpenIncidentCount,
            ThirdPartyProviderCount = r.ThirdPartyProviderCount,
            GapScore = r.GapScore,
            CyberAssessmentScore = r.CyberAssessmentScore,
            RcaCoveragePercent = r.RcaCoveragePercent,
            ImpactToleranceWatchCount = r.ImpactToleranceWatchCount,
            BusinessContinuityRiskCount = r.BusinessContinuityRiskCount,
            RecoveryTimeBreachCount = r.RecoveryTimeBreachCount,
            ChangeControlReviewCount = r.ChangeControlReviewCount,
            BoardSummary = r.BoardSummary
        };
    }

    public async Task<List<ImportantBusinessServiceRow>> GetBusinessServicesAsync(CancellationToken ct = default)
    {
        var ws = await _intelligence.GetWorkspaceAsync(ct);
        return ws.Resilience.BusinessServices;
    }

    public async Task<List<ImpactToleranceRow>> GetImpactTolerancesAsync(CancellationToken ct = default)
    {
        var ws = await _intelligence.GetWorkspaceAsync(ct);
        return ws.Resilience.ImpactTolerances;
    }

    public async Task<List<ThirdPartyProviderRiskRow>> GetThirdPartyRegisterAsync(CancellationToken ct = default)
    {
        var ws = await _intelligence.GetWorkspaceAsync(ct);
        return ws.Resilience.ThirdPartyRegister;
    }

    public async Task<ResilienceIncidentData> GetIncidentDataAsync(CancellationToken ct = default)
    {
        var ws = await _intelligence.GetWorkspaceAsync(ct);
        return new ResilienceIncidentData
        {
            Incidents = ws.Resilience.RecentIncidents,
            Timelines = ws.Resilience.IncidentTimelines,
            SecurityAlerts = ws.Resilience.RecentSecurityAlerts
        };
    }

    public async Task<ResilienceTestingData> GetTestingDataAsync(CancellationToken ct = default)
    {
        var ws = await _intelligence.GetWorkspaceAsync(ct);
        return new ResilienceTestingData
        {
            TestingSchedule = ws.Resilience.TestingSchedule,
            RecoveryTimeTests = ws.Resilience.RecoveryTimeTests,
            BusinessContinuityPlans = ws.Resilience.BusinessContinuityPlans
        };
    }

    public async Task<ResilienceBoardData> GetBoardDataAsync(CancellationToken ct = default)
    {
        var ws = await _intelligence.GetWorkspaceAsync(ct);
        var r = ws.Resilience;
        return new ResilienceBoardData
        {
            BoardSummary = r.BoardSummary,
            GapAnalysis = r.GapAnalysis,
            ActionTracker = r.ActionTracker,
            CyberAssessment = r.CyberAssessment,
            Hotspots = r.Hotspots
        };
    }
}

public sealed class ResilienceDashboardData
{
    public int ImportantServiceCount { get; set; }
    public int OpenIncidentCount { get; set; }
    public int ThirdPartyProviderCount { get; set; }
    public decimal GapScore { get; set; }
    public decimal CyberAssessmentScore { get; set; }
    public decimal RcaCoveragePercent { get; set; }
    public int ImpactToleranceWatchCount { get; set; }
    public int BusinessContinuityRiskCount { get; set; }
    public int RecoveryTimeBreachCount { get; set; }
    public int ChangeControlReviewCount { get; set; }
    public ResilienceBoardSummary BoardSummary { get; set; } = new();
}

public sealed class ResilienceIncidentData
{
    public List<ResilienceIncidentRow> Incidents { get; set; } = [];
    public List<ResilienceIncidentTimelineRow> Timelines { get; set; } = [];
    public List<SecurityAlertRow> SecurityAlerts { get; set; } = [];
}

public sealed class ResilienceTestingData
{
    public List<ResilienceTestingRow> TestingSchedule { get; set; } = [];
    public List<RecoveryTimeTestingRow> RecoveryTimeTests { get; set; } = [];
    public List<BusinessContinuityPlanRow> BusinessContinuityPlans { get; set; } = [];
}

public sealed class ResilienceBoardData
{
    public ResilienceBoardSummary BoardSummary { get; set; } = new();
    public List<ResilienceGapRow> GapAnalysis { get; set; } = [];
    public List<ResilienceActionRow> ActionTracker { get; set; } = [];
    public List<CyberResilienceAssessmentRow> CyberAssessment { get; set; } = [];
    public List<DependencyHotspotRow> Hotspots { get; set; } = [];
}
