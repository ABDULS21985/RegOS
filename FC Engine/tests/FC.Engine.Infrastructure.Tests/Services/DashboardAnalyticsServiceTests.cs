using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FC.Engine.Infrastructure.Tests.Services;

public class DashboardAnalyticsServiceTests
{
    [Fact]
    public async Task GetModuleDashboard_Returns_Period_Status_And_Trend_Data()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(nameof(GetModuleDashboard_Returns_Period_Status_And_Trend_Data));

        var module = new Module
        {
            Id = 101,
            ModuleCode = "FC",
            ModuleName = "FC Returns",
            RegulatorCode = "CBN",
            DefaultFrequency = "Monthly",
            CreatedAt = DateTime.UtcNow
        };
        db.Modules.Add(module);

        var periodJan = new ReturnPeriod
        {
            Id = 201,
            TenantId = tenantId,
            ModuleId = module.Id,
            Year = 2026,
            Month = 1,
            Frequency = "Monthly",
            ReportingDate = new DateTime(2026, 1, 31),
            DeadlineDate = new DateTime(2026, 2, 15),
            Status = "Completed",
            IsOpen = false,
            CreatedAt = DateTime.UtcNow
        };
        var periodFeb = new ReturnPeriod
        {
            Id = 202,
            TenantId = tenantId,
            ModuleId = module.Id,
            Year = 2026,
            Month = 2,
            Frequency = "Monthly",
            ReportingDate = new DateTime(2026, 2, 28),
            DeadlineDate = new DateTime(2026, 3, 15),
            Status = "Overdue",
            IsOpen = true,
            CreatedAt = DateTime.UtcNow
        };
        db.ReturnPeriods.AddRange(periodJan, periodFeb);

        var submissionJan = new Submission
        {
            Id = 301,
            TenantId = tenantId,
            InstitutionId = 10,
            ReturnPeriodId = periodJan.Id,
            ReturnCode = "FC_RET_001",
            Status = SubmissionStatus.Accepted,
            SubmittedAt = new DateTime(2026, 2, 10),
            CreatedAt = DateTime.UtcNow
        };
        db.Submissions.Add(submissionJan);

        var reportJan = new ValidationReport
        {
            Id = 401,
            TenantId = tenantId,
            SubmissionId = submissionJan.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.ValidationReports.Add(reportJan);
        db.ValidationErrors.Add(new ValidationError
        {
            ValidationReportId = reportJan.Id,
            RuleId = "R-001",
            Field = "amount",
            Message = "sample error",
            Severity = ValidationSeverity.Error,
            Category = ValidationCategory.TypeRange
        });

        db.FilingSlaRecords.AddRange(
            new FilingSlaRecord
            {
                TenantId = tenantId,
                ModuleId = module.Id,
                PeriodId = periodJan.Id,
                SubmissionId = submissionJan.Id,
                PeriodEndDate = periodJan.ReportingDate,
                DeadlineDate = periodJan.DeadlineDate,
                SubmittedDate = submissionJan.SubmittedAt,
                DaysToDeadline = 5,
                OnTime = true
            },
            new FilingSlaRecord
            {
                TenantId = tenantId,
                ModuleId = module.Id,
                PeriodId = periodFeb.Id,
                PeriodEndDate = periodFeb.ReportingDate,
                DeadlineDate = periodFeb.DeadlineDate,
                SubmittedDate = periodFeb.DeadlineDate.AddDays(2),
                DaysToDeadline = -2,
                OnTime = false
            });

        await db.SaveChangesAsync();

        var entitlement = BuildEntitlement(tenantId, module);
        var entitlementSvc = new Mock<IEntitlementService>();
        entitlementSvc
            .Setup(x => x.ResolveEntitlements(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entitlement);

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new DashboardService(db, cache, entitlementSvc.Object, NullLogger<DashboardService>.Instance);

        var result = await sut.GetModuleDashboard(tenantId, "FC");

        result.ModuleCode.Should().Be("FC");
        result.ModuleName.Should().Be("FC Returns");
        result.Periods.Should().HaveCount(2);
        result.Periods.Should().Contain(x => x.Status == "Submitted");
        result.Periods.Should().Contain(x => x.Status == "Overdue");
        result.ValidationErrorTrend.Datasets.Should().HaveCount(2);
        result.SubmissionTimelinessTrend.Datasets.Should().HaveCount(2);
        result.DataQualityTrend.Datasets.Should().HaveCount(2);
        result.FilingStatusBreakdown.Labels.Should().Contain(new[] { "Submitted", "Pending", "Overdue" });
    }

    [Fact]
    public async Task GetSubmissionTrend_Throws_When_Tenant_Not_Entitled_To_Module()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(nameof(GetSubmissionTrend_Throws_When_Tenant_Not_Entitled_To_Module));

        db.Modules.Add(new Module
        {
            Id = 111,
            ModuleCode = "FC",
            ModuleName = "FC Returns",
            RegulatorCode = "CBN",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var entitlementSvc = new Mock<IEntitlementService>();
        entitlementSvc
            .Setup(x => x.ResolveEntitlements(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantEntitlement
            {
                TenantId = tenantId,
                TenantStatus = TenantStatus.Active,
                ActiveModules = Array.Empty<EntitledModule>()
            });

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new DashboardService(db, cache, entitlementSvc.Object, NullLogger<DashboardService>.Instance);

        var act = async () => await sut.GetSubmissionTrend(tenantId, "FC");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not entitled*");
    }

    [Fact]
    public async Task GetSummary_Uses_Cache_For_Repeated_Requests()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(nameof(GetSummary_Uses_Cache_For_Repeated_Requests));

        var module = new Module
        {
            Id = 121,
            ModuleCode = "NDIC",
            ModuleName = "NDIC Returns",
            RegulatorCode = "NDIC",
            CreatedAt = DateTime.UtcNow
        };
        db.Modules.Add(module);
        db.ReturnPeriods.Add(new ReturnPeriod
        {
            TenantId = tenantId,
            ModuleId = module.Id,
            Year = 2026,
            Month = 2,
            Frequency = "Monthly",
            ReportingDate = new DateTime(2026, 2, 28),
            DeadlineDate = new DateTime(2026, 3, 15),
            Status = "Overdue",
            IsOpen = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var entitlementSvc = new Mock<IEntitlementService>();
        entitlementSvc
            .Setup(x => x.ResolveEntitlements(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildEntitlement(tenantId, module));

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new DashboardService(db, cache, entitlementSvc.Object, NullLogger<DashboardService>.Instance);

        var first = await sut.GetSummary(tenantId);
        var second = await sut.GetSummary(tenantId);

        second.GeneratedAt.Should().Be(first.GeneratedAt);
        second.Should().BeSameAs(first);
    }

    [Fact]
    public async Task GetPartnerDashboard_Returns_Portfolio_Revenue_And_Filing_Health()
    {
        await using var db = CreateDb(nameof(GetPartnerDashboard_Returns_Portfolio_Revenue_And_Filing_Health));

        var partner = Tenant.Create("Partner", "partner", TenantType.WhiteLabelPartner, "partner@test.local");
        partner.Activate();
        db.Tenants.Add(partner);

        var child = Tenant.Create("Child", "child", TenantType.Institution, "child@test.local");
        child.SetParentTenant(partner.TenantId);
        child.Activate();
        db.Tenants.Add(child);

        var module = new Module
        {
            Id = 331,
            ModuleCode = "FC_RETURNS",
            ModuleName = "FC Returns",
            RegulatorCode = "CBN",
            DefaultFrequency = "Monthly",
            CreatedAt = DateTime.UtcNow
        };

        var plan = new SubscriptionPlan
        {
            Id = 441,
            PlanCode = "ENTERPRISE",
            PlanName = "Enterprise",
            Tier = 3,
            MaxModules = 10,
            MaxUsersPerEntity = 100,
            MaxEntities = 100,
            MaxApiCallsPerMonth = 10000,
            MaxStorageMb = 10000,
            BasePriceMonthly = 100000,
            BasePriceAnnual = 1000000,
            IsActive = true
        };

        db.Modules.Add(module);
        db.SubscriptionPlans.Add(plan);

        db.Subscriptions.Add(new Subscription
        {
            Id = 551,
            TenantId = child.TenantId,
            PlanId = plan.Id,
            BillingFrequency = BillingFrequency.Monthly,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(20),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.SubscriptionModules.Add(new SubscriptionModule
        {
            SubscriptionId = 551,
            ModuleId = module.Id,
            PriceMonthly = 0,
            PriceAnnual = 0,
            IsActive = true
        });

        db.Institutions.Add(new Institution
        {
            Id = 601,
            TenantId = child.TenantId,
            InstitutionCode = "CHILD001",
            InstitutionName = "Child Institution",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        db.InstitutionUsers.Add(new InstitutionUser
        {
            TenantId = child.TenantId,
            InstitutionId = 601,
            Username = "child.admin",
            Email = "child.admin@test.local",
            DisplayName = "Child Admin",
            PasswordHash = "salt:hash",
            Role = InstitutionRole.Admin,
            IsActive = true,
            LastLoginAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        db.Submissions.Add(new Submission
        {
            TenantId = child.TenantId,
            InstitutionId = 601,
            ReturnPeriodId = 701,
            ReturnCode = "FC_RET_001",
            Status = SubmissionStatus.Accepted,
            SubmittedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });

        db.ReturnPeriods.Add(new ReturnPeriod
        {
            Id = 701,
            TenantId = child.TenantId,
            ModuleId = module.Id,
            Year = DateTime.UtcNow.Year,
            Month = DateTime.UtcNow.Month,
            Frequency = "Monthly",
            ReportingDate = DateTime.UtcNow.Date,
            DeadlineDate = DateTime.UtcNow.Date.AddDays(-1),
            Status = "Overdue",
            IsOpen = true,
            CreatedAt = DateTime.UtcNow
        });

        db.PartnerConfigs.Add(new PartnerConfig
        {
            TenantId = partner.TenantId,
            PartnerTier = PartnerTier.Gold,
            BillingModel = PartnerBillingModel.Direct,
            CommissionRate = 0.15m,
            MaxSubTenants = 20,
            AgreementSignedAt = DateTime.UtcNow,
            AgreementVersion = "v1"
        });

        db.PartnerRevenueRecords.Add(new PartnerRevenueRecord
        {
            TenantId = child.TenantId,
            PartnerTenantId = partner.TenantId,
            InvoiceId = 0,
            BillingModel = PartnerBillingModel.Direct,
            GrossAmount = 100000m,
            NetAmount = 100000m,
            CommissionRate = 0.15m,
            CommissionAmount = 15000m,
            PeriodStart = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-30)),
            PeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var entitlementSvc = new Mock<IEntitlementService>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new DashboardService(db, cache, entitlementSvc.Object, NullLogger<DashboardService>.Instance);

        var result = await sut.GetPartnerDashboard(partner.TenantId);

        result.Portfolio.TotalSubTenants.Should().Be(1);
        result.Usage.ActiveUsers.Should().Be(1);
        result.Revenue.CommissionsEarned.Should().Be(15000m);
        result.FilingHealth.Red.Should().BeGreaterThan(0);
    }

    private static TenantEntitlement BuildEntitlement(Guid tenantId, Module module)
    {
        return new TenantEntitlement
        {
            TenantId = tenantId,
            TenantStatus = TenantStatus.Active,
            ActiveModules =
            [
                new EntitledModule
                {
                    ModuleId = module.Id,
                    ModuleCode = module.ModuleCode,
                    ModuleName = module.ModuleName,
                    RegulatorCode = module.RegulatorCode,
                    IsActive = true,
                    IsRequired = true,
                    SheetCount = 1,
                    DefaultFrequency = module.DefaultFrequency
                }
            ]
        };
    }

    private static MetadataDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new MetadataDbContext(options);
    }
}
