using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Models;

public class RegulatorInboxFilter
{
    public string? InstitutionName { get; set; }
    public string? LicenceType { get; set; }
    public string? ModuleCode { get; set; }
    public string? PeriodCode { get; set; }
    public string? Status { get; set; }
}

public class RegulatorSubmissionInboxItem
{
    public int SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public int InstitutionId { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
    public string LicenceType { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public string SubmissionStatus { get; set; } = string.Empty;
    public RegulatorReceiptStatus ReceiptStatus { get; set; } = RegulatorReceiptStatus.Received;
    public int OpenQueryCount { get; set; }
}

public class RegulatorSubmissionDetail
{
    public RegulatorSubmissionInboxItem Header { get; set; } = new();
    public RegulatorReceipt? Receipt { get; set; }
    public List<ExaminerQuery> Queries { get; set; } = new();
    public List<ValidationErrorAggregate> TopValidationErrors { get; set; } = new();
}

public class SectorCarDistribution
{
    public string PeriodCode { get; set; } = string.Empty;
    public decimal AverageCar { get; set; }
    public decimal MedianCar { get; set; }
    public int InstitutionCount { get; set; }
    public List<HistogramBucket> Buckets { get; set; } = new();
}

public class HistogramBucket
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class SectorNplTrend
{
    public List<string> PeriodLabels { get; set; } = new();
    public List<decimal> AverageNplRatios { get; set; } = new();
}

public class SectorDepositStructure
{
    public string PeriodCode { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public List<DepositSlice> Slices { get; set; } = new();
}

public class DepositSlice
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class FilingTimeliness
{
    public string PeriodCode { get; set; } = string.Empty;
    public int OnTimeCount { get; set; }
    public int LateCount { get; set; }
    public List<InstitutionTimelinessItem> Institutions { get; set; } = new();
}

public class InstitutionTimelinessItem
{
    public int InstitutionId { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
    public int OnTime { get; set; }
    public int Late { get; set; }
}

public class FilingHeatmap
{
    public string PeriodCode { get; set; } = string.Empty;
    public List<string> Institutions { get; set; } = new();
    public List<string> Modules { get; set; } = new();
    public List<FilingHeatmapCell> Cells { get; set; } = new();
}

public class FilingHeatmapCell
{
    public string Institution { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public bool Filed { get; set; }
}

public class EntityBenchmarkResult
{
    public int InstitutionId { get; set; }
    public string InstitutionName { get; set; } = string.Empty;

    public decimal CarValue { get; set; }
    public decimal CarPeerAverage { get; set; }
    public decimal CarPeerMedian { get; set; }
    public decimal CarPeerP25 { get; set; }
    public decimal CarPeerP75 { get; set; }

    public decimal NplValue { get; set; }
    public decimal NplPeerAverage { get; set; }

    public decimal TimelinessScore { get; set; }
    public decimal TimelinessPeerAverage { get; set; }

    public decimal DataQualityScore { get; set; }
    public decimal DataQualityPeerAverage { get; set; }
}

public class EarlyWarningFlag
{
    public int InstitutionId { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
    public EarlyWarningSeverity Severity { get; set; }
    public string FlagCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
}

public class ExaminationProjectCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public List<int> InstitutionIds { get; set; } = new();
    public List<string> ModuleCodes { get; set; } = new();
    public DateTime? PeriodFrom { get; set; }
    public DateTime? PeriodTo { get; set; }
}

public class ExaminationWorkspaceData
{
    public ExaminationProject Project { get; set; } = new();
    public List<RegulatorSubmissionInboxItem> Submissions { get; set; } = new();
    public List<ExaminationAnnotation> Annotations { get; set; } = new();
    public Dictionary<int, EntityBenchmarkResult> BenchmarksByInstitution { get; set; } = new();
}
