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

    /// <summary>
    /// Looks up a whistleblower case by its anonymous HMAC token.
    /// <para>
    /// DESIGN NOTE — null tenant context: this method intentionally opens the database
    /// connection without a tenant identifier (passing <c>null</c> to
    /// <see cref="IDbConnectionFactory.CreateConnectionAsync"/>). The row-level security
    /// function <c>dbo.fn_TenantFilter</c> explicitly permits rows where
    /// <c>@TenantId IS NULL</c>, enabling a cross-tenant lookup that is safe here because
    /// (a) the anonymous token is a 64-character HMAC-SHA-256 hex digest — effectively
    /// unguessable — and (b) the query selects only the three non-sensitive status fields
    /// (CaseReference, Status, ReceivedAt, UpdatedAt). No PII or case detail is exposed.
    /// If the RLS policy is ever tightened to require a non-null session context, this
    /// method will silently return <c>null</c> for every token; add an integration test
    /// that covers the null-tenant path before making that change.
    /// </para>
    /// </summary>
    Task<WhistleblowerStatusView?> CheckStatusAsync(
        string anonymousToken,
        CancellationToken ct = default);

    Task<IReadOnlyList<WhistleblowerCaseSummary>> GetOpenCasesAsync(
        string regulatorCode,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all active portal users that belong to the regulator's tenant and can be
    /// assigned as case handlers.
    /// </summary>
    Task<IReadOnlyList<WhistleblowerAssignableUser>> GetAssignableUsersAsync(
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
