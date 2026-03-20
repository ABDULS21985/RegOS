using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FC.Engine.Infrastructure.Tests.Services;

public class FeatureFlagServiceTests
{
    private static MetadataDbContext CreateDb(string? name = null)
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new MetadataDbContext(options);
    }

    private static FeatureFlagService CreateService(MetadataDbContext db)
    {
        return new FeatureFlagService(db, new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public async Task IsEnabled_Returns_False_For_Unknown_Flag()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var result = await svc.IsEnabled("nonexistent");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabled_Returns_Global_Enabled_State()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag
        {
            FlagCode = "dashboard-v2",
            Description = "New dashboard",
            IsEnabled = true,
            RolloutPercent = 0
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        (await svc.IsEnabled("dashboard-v2")).Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabled_Returns_False_When_Globally_Disabled()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag
        {
            FlagCode = "beta-feature",
            Description = "Beta",
            IsEnabled = false,
            RolloutPercent = 0
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        (await svc.IsEnabled("beta-feature")).Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabled_Matches_Specific_Tenant_In_AllowedTenants()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag
        {
            FlagCode = "tenant-flag",
            Description = "Per-tenant",
            IsEnabled = false,
            RolloutPercent = 0,
            AllowedTenants = $"[\"{tenantId}\"]"
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        (await svc.IsEnabled("tenant-flag", tenantId)).Should().BeTrue();
        (await svc.IsEnabled("tenant-flag", Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabled_Matches_Plan_Based_Targeting()
    {
        await using var db = CreateDb();

        var tenant = Tenant.Create("PlanTest", "plantest", TenantType.Institution, "plan@test.com");
        tenant.Activate();
        db.Tenants.Add(tenant);

        var plan = new SubscriptionPlan
        {
            PlanCode = "ENTERPRISE",
            PlanName = "Enterprise",
            Tier = 3,
            MaxModules = 10,
            MaxUsersPerEntity = 50,
            MaxEntities = 10,
            MaxApiCallsPerMonth = 0,
            MaxStorageMb = 5000,
            BasePriceMonthly = 750000,
            BasePriceAnnual = 7500000,
            IsActive = true,
            DisplayOrder = 1
        };
        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync();

        var subscription = new Subscription
        {
            TenantId = tenant.TenantId,
            PlanId = plan.Id,
            BillingFrequency = BillingFrequency.Monthly,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15)
        };
        subscription.Activate();
        db.Subscriptions.Add(subscription);

        db.FeatureFlags.Add(new FeatureFlag
        {
            FlagCode = "plan-flag",
            Description = "Plan-based",
            IsEnabled = false,
            RolloutPercent = 0,
            AllowedPlans = "[\"ENTERPRISE\"]"
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        (await svc.IsEnabled("plan-flag", tenant.TenantId)).Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabled_Does_Not_Grant_Plan_Targeting_For_Suspended_Subscription()
    {
        await using var db = CreateDb();

        var tenant = Tenant.Create("Suspended Plan", "suspended-plan", TenantType.Institution, "suspended@test.com");
        tenant.Activate();
        db.Tenants.Add(tenant);

        var plan = new SubscriptionPlan
        {
            PlanCode = "ENTERPRISE",
            PlanName = "Enterprise",
            Tier = 3,
            MaxModules = 10,
            MaxUsersPerEntity = 50,
            MaxEntities = 10,
            MaxApiCallsPerMonth = 0,
            MaxStorageMb = 5000,
            BasePriceMonthly = 750000,
            BasePriceAnnual = 7500000,
            IsActive = true,
            DisplayOrder = 1
        };
        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync();

        var subscription = new Subscription
        {
            TenantId = tenant.TenantId,
            PlanId = plan.Id,
            BillingFrequency = BillingFrequency.Monthly,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15)
        };
        subscription.Activate();
        subscription.Suspend("payment overdue");
        db.Subscriptions.Add(subscription);

        db.FeatureFlags.Add(new FeatureFlag
        {
            FlagCode = "plan-flag",
            Description = "Plan-based",
            IsEnabled = false,
            RolloutPercent = 0,
            AllowedPlans = "[\"ENTERPRISE\"]"
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        (await svc.IsEnabled("plan-flag", tenant.TenantId)).Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabled_Rollout_Percent_Is_Deterministic()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag
        {
            FlagCode = "rollout-flag",
            Description = "Rollout",
            IsEnabled = false,
            RolloutPercent = 100
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var tenantId = Guid.NewGuid();

        // 100% rollout should always be enabled
        (await svc.IsEnabled("rollout-flag", tenantId)).Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabled_Zero_Rollout_Does_Not_Enable()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag
        {
            FlagCode = "no-rollout",
            Description = "No rollout",
            IsEnabled = false,
            RolloutPercent = 0
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        (await svc.IsEnabled("no-rollout", Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Upsert_Creates_New_Flag()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var result = await svc.Upsert("new-flag", "A new flag", true, 50, null, null);

        result.FlagCode.Should().Be("new-flag");
        result.IsEnabled.Should().BeTrue();
        result.RolloutPercent.Should().Be(50);

        var persisted = await db.FeatureFlags.SingleAsync();
        persisted.FlagCode.Should().Be("new-flag");
    }

    [Fact]
    public async Task Upsert_Updates_Existing_Flag()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag
        {
            FlagCode = "existing-flag",
            Description = "Old description",
            IsEnabled = false,
            RolloutPercent = 0
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        var result = await svc.Upsert("existing-flag", "New description", true, 75, null, "[\"STARTER\"]");

        result.Description.Should().Be("New description");
        result.IsEnabled.Should().BeTrue();
        result.RolloutPercent.Should().Be(75);
        result.AllowedPlans.Should().Contain("STARTER");

        (await db.FeatureFlags.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Upsert_Normalizes_Flag_Code_To_Lowercase()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        await svc.Upsert("MY-FLAG", "Test", false, 0, null, null);

        var flag = await db.FeatureFlags.SingleAsync();
        flag.FlagCode.Should().Be("my-flag");
    }

    [Fact]
    public async Task Upsert_Clamps_Rollout_Percent()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var over = await svc.Upsert("over", "Test", false, 150, null, null);
        over.RolloutPercent.Should().Be(100);

        var under = await svc.Upsert("under", "Test", false, -10, null, null);
        under.RolloutPercent.Should().Be(0);
    }

    [Fact]
    public async Task GetAll_Returns_All_Flags_Ordered()
    {
        await using var db = CreateDb();
        db.FeatureFlags.AddRange(
            new FeatureFlag { FlagCode = "z-flag", Description = "Z", IsEnabled = false, RolloutPercent = 0 },
            new FeatureFlag { FlagCode = "a-flag", Description = "A", IsEnabled = true, RolloutPercent = 0 }
        );
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var all = await svc.GetAll();

        all.Should().HaveCount(2);
        all[0].FlagCode.Should().Be("a-flag");
        all[1].FlagCode.Should().Be("z-flag");
    }

    [Fact]
    public async Task IsEnabled_Case_Insensitive_Flag_Code()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag
        {
            FlagCode = "my-feature",
            Description = "Test",
            IsEnabled = true,
            RolloutPercent = 0
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        (await svc.IsEnabled("MY-FEATURE")).Should().BeTrue();
        (await svc.IsEnabled("My-Feature")).Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabled_Csv_AllowedTenants_Works()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag
        {
            FlagCode = "csv-tenants",
            Description = "CSV format",
            IsEnabled = false,
            RolloutPercent = 0,
            AllowedTenants = $"{tenantId},{Guid.NewGuid()}"
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        (await svc.IsEnabled("csv-tenants", tenantId)).Should().BeTrue();
    }

    [Fact]
    public async Task Upsert_Throws_For_Empty_FlagCode()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var act = () => svc.Upsert("", "Empty", false, 0, null, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
