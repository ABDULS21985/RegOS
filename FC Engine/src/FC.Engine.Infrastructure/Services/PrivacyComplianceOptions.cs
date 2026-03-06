namespace FC.Engine.Infrastructure.Services;

public sealed class PrivacyComplianceOptions
{
    public const string SectionName = "PrivacyCompliance";

    public string PolicyVersion { get; set; } = "1.0";
    public int DsarDueDays { get; set; } = 30;
    public int RetentionYears { get; set; } = 7;
}
