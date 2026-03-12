using FC.Engine.Admin.Services;
using FC.Engine.Infrastructure.Services;

namespace FC.Engine.Admin.Services.Sanctions;

/// <summary>
/// Wraps PlatformIntelligenceService and SanctionsWorkflowStoreService to provide
/// alert management, disposition workflows, and false positive library access.
/// All data flows through real DB-backed services.
/// </summary>
public sealed class AlertWorkflowService
{
    private readonly PlatformIntelligenceService _intelligence;
    private readonly SanctionsWorkflowStoreService _workflowStore;
    private readonly SanctionsScreeningSessionStoreService _sessionStore;

    public AlertWorkflowService(
        PlatformIntelligenceService intelligence,
        SanctionsWorkflowStoreService workflowStore,
        SanctionsScreeningSessionStoreService sessionStore)
    {
        _intelligence = intelligence;
        _workflowStore = workflowStore;
        _sessionStore = sessionStore;
    }

    public async Task<AlertWorkflowData> GetWorkflowDataAsync(CancellationToken ct = default)
    {
        var workflow = await _workflowStore.LoadAsync(ct);
        var session = await _sessionStore.LoadLatestAsync(ct);

        var activeAlerts = new List<AlertItem>();
        var escalatedAlerts = new List<AlertItem>();
        var resolvedAlerts = new List<AlertItem>();

        // Build alerts from latest screening results
        if (session.LatestRun is not null)
        {
            foreach (var result in session.LatestRun.Results)
            {
                var decision = workflow.LatestDecisions
                    .FirstOrDefault(d => d.MatchKey == $"{result.Subject}|{result.MatchedName}|{result.SourceCode}");

                var alert = new AlertItem
                {
                    MatchKey = $"{result.Subject}|{result.MatchedName}|{result.SourceCode}",
                    Subject = result.Subject,
                    MatchedName = result.MatchedName,
                    SourceCode = result.SourceCode,
                    SourceName = result.SourceName,
                    Category = result.Category,
                    RiskLevel = result.RiskLevel,
                    MatchScore = result.MatchScore,
                    Disposition = result.Disposition,
                    Decision = decision?.Decision,
                    ReviewedAtUtc = decision?.ReviewedAtUtc,
                    ScreenedAt = session.LatestRun.ScreenedAt
                };

                if (decision is not null && decision.Decision == "False Positive")
                    resolvedAlerts.Add(alert);
                else if (result.Disposition == "True Match")
                    escalatedAlerts.Add(alert);
                else if (result.Disposition == "Potential Match")
                    activeAlerts.Add(alert);
            }
        }

        // Also include transaction-based alerts
        if (session.LatestTransaction is not null)
        {
            foreach (var party in session.LatestTransaction.PartyResults
                .Where(p => p.Disposition is "True Match" or "Potential Match"))
            {
                var matchKey = $"{party.PartyName}|{party.MatchedName}|{party.SourceCode}";
                var decision = workflow.LatestDecisions
                    .FirstOrDefault(d => d.MatchKey == matchKey);

                var alert = new AlertItem
                {
                    MatchKey = matchKey,
                    Subject = party.PartyName,
                    MatchedName = party.MatchedName,
                    SourceCode = party.SourceCode,
                    SourceName = party.SourceCode,
                    Category = party.PartyRole,
                    RiskLevel = party.RiskLevel,
                    MatchScore = party.MatchScore,
                    Disposition = party.Disposition,
                    Decision = decision?.Decision,
                    ReviewedAtUtc = decision?.ReviewedAtUtc,
                    ScreenedAt = DateTime.UtcNow,
                    TransactionReference = session.LatestTransaction.TransactionReference,
                    ControlDecision = session.LatestTransaction.ControlDecision
                };

                if (decision is not null && decision.Decision == "False Positive")
                    resolvedAlerts.Add(alert);
                else if (party.Disposition == "True Match")
                    escalatedAlerts.Add(alert);
                else
                    activeAlerts.Add(alert);
            }
        }

        return new AlertWorkflowData
        {
            ActiveAlerts = activeAlerts,
            EscalatedAlerts = escalatedAlerts,
            ResolvedAlerts = resolvedAlerts,
            FalsePositiveLibrary = workflow.FalsePositiveLibrary,
            AuditTrail = workflow.AuditTrail
        };
    }

    public async Task RecordDecisionAsync(
        string matchKey, string subject, string matchedName,
        string sourceCode, string riskLevel, string previousDecision,
        string decision, CancellationToken ct = default)
    {
        await _workflowStore.RecordDecisionAsync(new SanctionsWorkflowDecisionCommand
        {
            MatchKey = matchKey,
            Subject = subject,
            MatchedName = matchedName,
            SourceCode = sourceCode,
            RiskLevel = riskLevel,
            PreviousDecision = previousDecision,
            Decision = decision,
            ReviewedAtUtc = DateTime.UtcNow
        }, ct);
    }
}

public sealed class AlertWorkflowData
{
    public List<AlertItem> ActiveAlerts { get; set; } = [];
    public List<AlertItem> EscalatedAlerts { get; set; } = [];
    public List<AlertItem> ResolvedAlerts { get; set; } = [];
    public List<SanctionsWorkflowFalsePositiveRecord> FalsePositiveLibrary { get; set; } = [];
    public List<SanctionsWorkflowAuditRecord> AuditTrail { get; set; } = [];
}

public sealed class AlertItem
{
    public string MatchKey { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string MatchedName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public double MatchScore { get; set; }
    public string Disposition { get; set; } = string.Empty;
    public string? Decision { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime ScreenedAt { get; set; }
    public string? TransactionReference { get; set; }
    public string? ControlDecision { get; set; }
}
