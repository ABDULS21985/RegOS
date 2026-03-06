using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Models;

public class DashboardSummary
{
    public int ActiveModules { get; set; }
    public int OverdueReturns { get; set; }
    public int PendingReturns { get; set; }
    public decimal ComplianceScore { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class ModuleDashboardData
{
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public List<ModulePeriodStatusItem> Periods { get; set; } = new();
    public TrendData ValidationErrorTrend { get; set; } = new();
    public TrendData SubmissionTimelinessTrend { get; set; } = new();
    public TrendData DataQualityTrend { get; set; } = new();
    public TrendData FilingStatusBreakdown { get; set; } = new();
}

public class ModulePeriodStatusItem
{
    public int PeriodId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RagClass { get; set; } = "green";
    public decimal CompletionPercent { get; set; }
    public int ValidationErrorCount { get; set; }
    public int ValidationWarningCount { get; set; }
    public DateTime Deadline { get; set; }
    public bool OnTime { get; set; }
    public int? SubmissionId { get; set; }
    public SubmissionStatus? SubmissionStatus { get; set; }
}

public class ComplianceSummaryData
{
    public decimal OverallScore { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<ComplianceModuleRow> Modules { get; set; } = new();
}

public class ComplianceModuleRow
{
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string CurrentRag { get; set; } = "green";
    public string PreviousRag { get; set; } = "green";
    public string Trend { get; set; } = "Stable";
    public decimal Score { get; set; }
    public decimal TimelinessScore { get; set; }
    public decimal DataQualityScore { get; set; }
    public decimal ValidationPassRate { get; set; }
}

public class TrendData
{
    public string Title { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = new();
    public List<TrendDataset> Datasets { get; set; } = new();
}

public class TrendDataset
{
    public string Label { get; set; } = string.Empty;
    public List<decimal> Data { get; set; } = new();
    public string BackgroundColor { get; set; } = "#0F766E";
    public string BorderColor { get; set; } = "#0F766E";
}

public class BenchmarkResult
{
    public decimal TenantAverageDays { get; set; }
    public decimal PeerMedianDays { get; set; }
    public decimal PeerP25Days { get; set; }
    public decimal PeerP75Days { get; set; }
    public int Percentile { get; set; }
    public int PeerCount { get; set; }
}

public class AdminDashboardData
{
    public UserActivityMetrics UserActivity { get; set; } = new();
    public SubscriptionUsageMetrics Usage { get; set; } = new();
    public BillingSummaryMetrics Billing { get; set; } = new();
    public NotificationStats NotificationStats { get; set; } = new();
    public TrendData DataQualityTrend { get; set; } = new();
    public decimal ValidationPassRate { get; set; }
    public decimal CompletenessScore { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class UserActivityMetrics
{
    public int ActiveUsersThisMonth { get; set; }
    public decimal AverageLoginsPerUser { get; set; }
    public List<UserContributionItem> TopContributors { get; set; } = new();
}

public class UserContributionItem
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int SubmissionCount { get; set; }
}

public class SubscriptionUsageMetrics
{
    public int UsersUsed { get; set; }
    public int UsersLimit { get; set; }
    public decimal UsersUsagePercent { get; set; }
    public int EntitiesUsed { get; set; }
    public int EntitiesLimit { get; set; }
    public decimal EntitiesUsagePercent { get; set; }
    public int ModulesUsed { get; set; }
    public int ModulesLimit { get; set; }
    public decimal ModulesUsagePercent { get; set; }
    public decimal StorageUsedMb { get; set; }
    public int StorageLimitMb { get; set; }
    public decimal StorageUsagePercent { get; set; }
    public int ApiCallsUsed { get; set; }
    public int ApiCallsLimit { get; set; }
    public decimal ApiUsagePercent { get; set; }
}

public class BillingSummaryMetrics
{
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string BillingFrequency { get; set; } = string.Empty;
    public DateTime NextInvoiceDate { get; set; }
    public decimal OutstandingBalance { get; set; }
    public string Currency { get; set; } = "NGN";
}

public class NotificationStats
{
    public int Sent { get; set; }
    public int Delivered { get; set; }
    public int Failed { get; set; }
    public int Queued { get; set; }
}

public class PlatformDashboardData
{
    public PlatformTenantStats TenantStats { get; set; } = new();
    public RevenueMetrics Revenue { get; set; } = new();
    public List<ModuleAdoptionItem> ModuleAdoption { get; set; } = new();
    public PlatformHealthMetrics PlatformHealth { get; set; } = new();
    public FilingAnalyticsMetrics FilingAnalytics { get; set; } = new();
    public List<TopTenantUsageItem> TopTenantsByUsage { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class PlatformTenantStats
{
    public int TotalActiveTenants { get; set; }
    public int NewThisMonth { get; set; }
    public int ChurnedThisMonth { get; set; }
}

public class RevenueMetrics
{
    public decimal Mrr { get; set; }
    public decimal Arr { get; set; }
    public List<RevenueBreakdownItem> RevenueByPlan { get; set; } = new();
    public List<RevenueBreakdownItem> RevenueByModule { get; set; } = new();
}

public class RevenueBreakdownItem
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ModuleAdoptionItem
{
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public int ActiveTenants { get; set; }
    public decimal AdoptionRate { get; set; }
}

public class PlatformHealthMetrics
{
    public decimal ApiLatencyP50Ms { get; set; }
    public decimal ApiLatencyP95Ms { get; set; }
    public decimal ApiLatencyP99Ms { get; set; }
    public decimal ErrorRatePercent { get; set; }
    public int ActiveSessions { get; set; }
}

public class FilingAnalyticsMetrics
{
    public int TotalReturnsSubmittedThisPeriod { get; set; }
    public decimal OnTimeRatePercent { get; set; }
    public List<ValidationErrorAggregate> TopValidationErrors { get; set; } = new();
}

public class ValidationErrorAggregate
{
    public string RuleId { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class TopTenantUsageItem
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public int ReturnsSubmitted { get; set; }
}

public class PartnerDashboardData
{
    public PartnerPortfolioMetrics Portfolio { get; set; } = new();
    public PartnerRevenueMetrics Revenue { get; set; } = new();
    public PartnerUsageAggregate Usage { get; set; } = new();
    public List<PartnerChurnRiskItem> ChurnRisk { get; set; } = new();
    public PartnerFilingHealth FilingHealth { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class PartnerPortfolioMetrics
{
    public int TotalSubTenants { get; set; }
    public int ActiveSubTenants { get; set; }
    public List<RevenueBreakdownItem> PlanDistribution { get; set; } = new();
    public List<RevenueBreakdownItem> ModuleUsageDistribution { get; set; } = new();
}

public class PartnerRevenueMetrics
{
    public string BillingModel { get; set; } = string.Empty;
    public decimal GrossBilled { get; set; }
    public decimal CommissionsEarned { get; set; }
    public decimal WholesaleDiscountAmount { get; set; }
    public decimal NetPlatformRevenue { get; set; }
}

public class PartnerUsageAggregate
{
    public int ActiveUsers { get; set; }
    public int ActiveInstitutions { get; set; }
    public int SubmittedReturnsThisMonth { get; set; }
}

public class PartnerChurnRiskItem
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public decimal UsageChangePercent { get; set; }
    public DateTime? RenewalDate { get; set; }
}

public class PartnerFilingHealth
{
    public int Green { get; set; }
    public int Amber { get; set; }
    public int Red { get; set; }
}
