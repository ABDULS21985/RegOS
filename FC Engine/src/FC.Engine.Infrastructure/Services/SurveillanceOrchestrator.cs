using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.Services;

public sealed class SurveillanceOrchestrator : ISurveillanceOrchestrator
{
    private readonly IBDCFXSurveillance _bdc;
    private readonly ICMOSurveillance _cmo;
    private readonly IInsuranceConductMonitor _insurance;
    private readonly IAMLConductMonitor _aml;
    private readonly IConductRiskScorer _scorer;
    private readonly ILogger<SurveillanceOrchestrator> _log;

    public SurveillanceOrchestrator(
        IBDCFXSurveillance bdc,
        ICMOSurveillance cmo,
        IInsuranceConductMonitor insurance,
        IAMLConductMonitor aml,
        IConductRiskScorer scorer,
        ILogger<SurveillanceOrchestrator> log)
    {
        _bdc = bdc;
        _cmo = cmo;
        _insurance = insurance;
        _aml = aml;
        _scorer = scorer;
        _log = log;
    }

    public async Task<SurveillanceRunResult> RunCycleAsync(
        string regulatorCode,
        string periodCode,
        CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        var totalAlerts = 0;

        totalAlerts += await RunDetectorAsync(
            "BDC rate manipulation",
            () => _bdc.DetectRateManipulationAsync(regulatorCode, periodCode, runId, ct));
        totalAlerts += await RunDetectorAsync(
            "BDC volume spikes",
            () => _bdc.DetectVolumeSpikesAsync(regulatorCode, periodCode, runId, ct));
        totalAlerts += await RunDetectorAsync(
            "BDC wash trading",
            () => _bdc.DetectWashTradingAsync(regulatorCode, periodCode, runId, ct));

        totalAlerts += await RunDetectorAsync(
            "CMO unusual trading",
            () => _cmo.DetectUnusualTradingPatternsAsync(regulatorCode, periodCode, runId, ct));
        totalAlerts += await RunDetectorAsync(
            "CMO late reporting",
            () => _cmo.DetectLateReportingAsync(regulatorCode, periodCode, runId, ct));
        totalAlerts += await RunDetectorAsync(
            "CMO concentration",
            () => _cmo.DetectClientConcentrationAsync(regulatorCode, periodCode, runId, ct));

        totalAlerts += await RunDetectorAsync(
            "Insurance claims suppression",
            () => _insurance.DetectClaimsSuppressionAsync(regulatorCode, periodCode, runId, ct));
        totalAlerts += await RunDetectorAsync(
            "Insurance premium under-reporting",
            () => _insurance.DetectPremiumUnderReportingAsync(regulatorCode, periodCode, runId, ct));
        totalAlerts += await RunDetectorAsync(
            "Insurance related-party reinsurance",
            () => _insurance.DetectRelatedPartyReinsuranceAsync(regulatorCode, periodCode, runId, ct));

        totalAlerts += await RunDetectorAsync(
            "AML low STR filers",
            () => _aml.DetectLowSTRFilersAsync(regulatorCode, periodCode, runId, ct));
        totalAlerts += await RunDetectorAsync(
            "AML structuring patterns",
            () => _aml.DetectStructuringPatternsAsync(regulatorCode, periodCode, runId, ct));
        totalAlerts += await RunDetectorAsync(
            "AML TFS ineffectiveness",
            () => _aml.DetectTFSIneffectivenessAsync(regulatorCode, periodCode, runId, ct));

        var scores = await _scorer.ScoreSectorAsync(regulatorCode, periodCode, runId, ct);
        var duration = DateTimeOffset.UtcNow - started;

        _log.LogInformation(
            "Conduct surveillance cycle completed. Regulator={RegulatorCode} Period={PeriodCode} Alerts={Alerts} Entities={Entities} DurationMs={DurationMs}",
            regulatorCode,
            periodCode,
            totalAlerts,
            scores.Count,
            duration.TotalMilliseconds);

        return new SurveillanceRunResult(
            runId,
            regulatorCode,
            periodCode,
            totalAlerts,
            scores.Count,
            duration);
    }

    private async Task<int> RunDetectorAsync(string name, Func<Task<int>> detector)
    {
        try
        {
            return await detector();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Surveillance detector failed: {DetectorName}", name);
            return 0;
        }
    }
}
