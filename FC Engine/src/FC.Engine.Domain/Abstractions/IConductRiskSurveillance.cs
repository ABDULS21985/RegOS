using FC.Engine.Domain.Models;

namespace FC.Engine.Domain.Abstractions;

public interface ISurveillanceOrchestrator
{
    Task<SurveillanceRunResult> RunCycleAsync(
        string regulatorCode,
        string periodCode,
        CancellationToken ct = default);
}

public interface IBDCFXSurveillance
{
    Task<int> DetectRateManipulationAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default);

    Task<int> DetectVolumeSpikesAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default);

    Task<int> DetectWashTradingAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default);
}

public interface ICMOSurveillance
{
    Task<int> DetectUnusualTradingPatternsAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default);

    Task<int> DetectLateReportingAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default);

    Task<int> DetectClientConcentrationAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default);
}

public interface IInsuranceConductMonitor
{
    Task<int> DetectClaimsSuppressionAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default);

    Task<int> DetectPremiumUnderReportingAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default);

    Task<int> DetectRelatedPartyReinsuranceAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default);
}

public interface IAMLConductMonitor
{
    Task<int> DetectLowSTRFilersAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default);

    Task<int> DetectStructuringPatternsAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default);

    Task<int> DetectTFSIneffectivenessAsync(
        string regulatorCode,
        string periodCode,
        Guid runId,
        CancellationToken ct = default);
}

public interface IConductRiskScorer
{
    Task<ConductScoreComponents> ScoreInstitutionAsync(
        int institutionId,
        string regulatorCode,
        string periodCode,
        Guid computationRunId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ConductScoreComponents>> ScoreSectorAsync(
        string regulatorCode,
        string periodCode,
        Guid computationRunId,
        CancellationToken ct = default);
}

public interface IWhistleblowerService
{
    Task<WhistleblowerSubmissionReceipt> SubmitAsync(
        WhistleblowerSubmission submission,
        CancellationToken ct = default);

    Task<WhistleblowerStatusView?> CheckStatusAsync(
        string anonymousToken,
        CancellationToken ct = default);

    Task<IReadOnlyList<WhistleblowerCaseSummary>> GetOpenCasesAsync(
        string regulatorCode,
        CancellationToken ct = default);

    Task AssignCaseAsync(
        string caseReference,
        int assignedUserId,
        CancellationToken ct = default);

    Task UpdateStatusAsync(
        string caseReference,
        WhistleblowerStatus newStatus,
        string note,
        int performedByUserId,
        CancellationToken ct = default);
}

public interface IAlertManagementService
{
    Task ResolveAlertAsync(AlertResolution resolution, CancellationToken ct = default);

    Task<IReadOnlyList<SurveillanceAlertRow>> GetOpenAlertsAsync(
        string regulatorCode,
        AlertCategory? category,
        AlertSeverity? minSeverity,
        CancellationToken ct = default);
}
