namespace FC.Engine.Domain.Models;

public enum AlertCategory
{
    BdcFx,
    Cmo,
    Insurance,
    Aml,
    Conduct
}

public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum ConductRiskBand
{
    Low,
    Medium,
    High,
    Critical
}

public enum WhistleblowerCategory
{
    FxManipulation,
    InsiderTrading,
    AmlFailure,
    PremiumFraud,
    ClaimsSuppression,
    RelatedPartyAbuse,
    Other
}

public enum WhistleblowerStatus
{
    Received,
    UnderReview,
    Concluded,
    Referred,
    Closed
}

public sealed record BDCRateAnomalyEvidence(
    decimal ObservedRate,
    decimal CBNMidRate,
    decimal CBNBandUpper,
    decimal CBNBandLower,
    decimal DeviationPct,
    int ConsecutiveDaysOutside,
    DateOnly FirstBreachDate
);

public sealed record VolumeAnomalyEvidence(
    decimal TodayVolumeUSD,
    decimal BaselineAvgUSD,
    decimal BaselineStdDevUSD,
    double ZScore,
    DateOnly ObservationDate
);

public sealed record WashTradeEvidence(
    int LendingInstitutionId,
    int BorrowingInstitutionId,
    decimal TotalCircularAmountUSD,
    int TransactionCount,
    DateOnly WindowStart,
    DateOnly WindowEnd
);

public sealed record CMOUnusualTradeEvidence(
    string SecurityCode,
    string SecurityName,
    decimal TodayVolume,
    decimal BaselineAvgVolume,
    double VolumeMultiple,
    int DaysBeforeAnnouncement
);

public sealed record AMLConductEvidence(
    decimal STRZScore,
    decimal PeerAvgSTRCount,
    int InstitutionSTRCount,
    decimal TFSFalsePositiveRate,
    int StructuringAlertCount
);

public sealed record InsuranceConductEvidence(
    decimal ClaimsRatio,
    decimal PeerAvgClaimsRatio,
    decimal Deviation,
    decimal PremiumGapNGN,
    decimal RelatedPartyReinsurancePct
);

public sealed record ConductScoreComponents(
    int InstitutionId,
    string PeriodCode,
    double MarketAbuseScore,
    double AMLEffectivenessScore,
    double InsuranceConductScore,
    double CustomerConductScore,
    double GovernanceScore,
    double SanctionHistoryScore,
    double CompositeScore,
    ConductRiskBand RiskBand,
    int ActiveAlertCount
);

public sealed record WhistleblowerSubmission(
    string RegulatorCode,
    int? AllegedInstitutionId,
    string? AllegedInstitutionName,
    WhistleblowerCategory Category,
    string Summary,
    string? EvidenceDescription,
    IReadOnlyList<string> EvidenceFileKeys
);

public sealed record WhistleblowerSubmissionReceipt(
    string CaseReference,
    string AnonymousToken,
    DateTimeOffset ReceivedAt,
    string StatusCheckUrl
);

public sealed record AlertResolution(
    long AlertId,
    int ResolvedByUserId,
    string Outcome,
    string Note
);

public sealed record SurveillanceRunResult(
    Guid RunId,
    string RegulatorCode,
    string PeriodCode,
    int AlertsRaised,
    int EntitiesScored,
    TimeSpan Duration
);

public sealed record WhistleblowerStatusView(
    string CaseReference,
    string Status,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? UpdatedAt
);

public sealed record WhistleblowerAssignableUser(
    int UserId,
    string DisplayName
);

public sealed record WhistleblowerCaseSummary(
    long ReportId,
    string CaseReference,
    string Category,
    string? AllegedInstitutionName,
    string Status,
    int PriorityScore,
    string? AssignedToUserName,
    DateTimeOffset ReceivedAt
);

public sealed record SurveillanceAlertRow(
    long AlertId,
    string AlertCode,
    string Category,
    string Severity,
    string Title,
    string? Detail,
    string? EvidenceJson,
    int? InstitutionId,
    string? InstitutionName,
    DateTimeOffset DetectedAt,
    bool IsResolved
);
