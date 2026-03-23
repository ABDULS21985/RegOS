using FC.Engine.Admin.Services.Capital;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FC.Engine.Admin.Tests.Services;

public class CapitalStackOptimizerServiceTests
{
    [Fact]
    public async Task OptimizeAsync_Returns_Correct_Mix_With_Defaults()
    {
        await using var db = CreateDb();
        var sut = new CapitalStackOptimizerService(new CapitalPlanningScenarioStoreService(db));

        var result = await sut.OptimizeAsync(new StackOptimizationRequest
        {
            TargetCarPercent = 15m,
            CurrentRwaBn = 100m,
            Cet1CostPercent = 12m,
            At1CostPercent = 8m,
            Tier2CostPercent = 5m,
            MaxAt1SharePercent = 30m,
            MaxTier2SharePercent = 35m
        });

        result.RequiredCapitalBn.Should().Be(15m);
        result.BlendedCostPercent.Should().BeLessThan(12m); // Must be cheaper than CET1-only
        result.CostSavingBps.Should().BeGreaterThan(0);

        // Shares should sum to ~100%
        var totalShare = result.Cet1SharePercent + result.At1SharePercent + result.Tier2SharePercent;
        totalShare.Should().BeApproximately(100m, 0.5m);

        // CET1 must be at least 4.5% of RWA (4.5 bn)
        result.Cet1Bn.Should().BeGreaterOrEqualTo(4.5m);

        // AT1 should not exceed max share
        result.At1SharePercent.Should().BeLessOrEqualTo(30m);
        result.Tier2SharePercent.Should().BeLessOrEqualTo(35m);
    }

    [Fact]
    public async Task OptimizeAsync_Uses_Scenario_Defaults_When_Request_Has_Zeros()
    {
        await using var db = CreateDb();
        await SeedScenario(db);
        var sut = new CapitalStackOptimizerService(new CapitalPlanningScenarioStoreService(db));

        var result = await sut.OptimizeAsync(new StackOptimizationRequest());

        result.TargetCarPercent.Should().Be(18m); // From seeded scenario
        result.CurrentRwaBn.Should().Be(120m);
        result.ScenarioSavedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task OptimizeAsync_Enforces_Basel_CET1_Minimum()
    {
        await using var db = CreateDb();
        var sut = new CapitalStackOptimizerService(new CapitalPlanningScenarioStoreService(db));

        // Request with very high AT1/Tier2 shares to test CET1 floor
        var result = await sut.OptimizeAsync(new StackOptimizationRequest
        {
            TargetCarPercent = 8m, // Low CAR
            CurrentRwaBn = 100m,
            Cet1CostPercent = 12m,
            At1CostPercent = 8m,
            Tier2CostPercent = 5m,
            MaxAt1SharePercent = 50m,
            MaxTier2SharePercent = 50m
        });

        // CET1 must be >= 4.5% of 100bn RWA = 4.5bn
        result.Cet1Bn.Should().BeGreaterOrEqualTo(4.5m);
    }

    [Fact]
    public async Task GetCostFrontierAsync_Returns_35_Points_From_8_To_25_Percent()
    {
        await using var db = CreateDb();
        var sut = new CapitalStackOptimizerService(new CapitalPlanningScenarioStoreService(db));

        var frontier = await sut.GetCostFrontierAsync(new StackOptimizationRequest
        {
            CurrentRwaBn = 100m,
            Cet1CostPercent = 12m,
            At1CostPercent = 8m,
            Tier2CostPercent = 5m,
            MaxAt1SharePercent = 30m,
            MaxTier2SharePercent = 35m
        });

        frontier.Should().HaveCount(35); // 8.0, 8.5, ..., 25.0
        frontier.First().CarPercent.Should().Be(8.0m);
        frontier.Last().CarPercent.Should().Be(25.0m);

        // All blended costs should be <= CET1-only cost
        foreach (var point in frontier)
        {
            point.BlendedCostPercent.Should().BeLessOrEqualTo(point.Cet1OnlyCostPercent);
        }
    }

    [Fact]
    public async Task GetCostFrontierAsync_Enforces_Basel_CET1_Floor_At_All_Points()
    {
        await using var db = CreateDb();
        var sut = new CapitalStackOptimizerService(new CapitalPlanningScenarioStoreService(db));

        var frontier = await sut.GetCostFrontierAsync(new StackOptimizationRequest
        {
            CurrentRwaBn = 100m,
            Cet1CostPercent = 12m,
            At1CostPercent = 8m,
            Tier2CostPercent = 5m,
            MaxAt1SharePercent = 50m,
            MaxTier2SharePercent = 50m
        });

        foreach (var point in frontier)
        {
            // CET1 amount = RequiredCapital * CET1Share / 100
            var cet1Bn = point.RequiredCapitalBn * point.Cet1SharePercent / 100m;
            // Must be >= 4.5% of RWA = 4.5bn
            cet1Bn.Should().BeGreaterOrEqualTo(4.4m, // small tolerance for rounding
                $"At CAR={point.CarPercent}%, CET1={cet1Bn}bn should meet Basel 4.5% floor");
        }
    }

    private static async Task SeedScenario(MetadataDbContext db)
    {
        var store = new CapitalPlanningScenarioStoreService(db);
        await store.SaveAsync(new CapitalPlanningScenarioCommand
        {
            CurrentCarPercent = 18m,
            CurrentRwaBn = 120m,
            QuarterlyRwaGrowthPercent = 2m,
            QuarterlyRetainedEarningsBn = 0.5m,
            MinimumRequirementPercent = 10m,
            ConservationBufferPercent = 2.5m,
            CountercyclicalBufferPercent = 0.5m,
            DsibBufferPercent = 1m,
            TargetCarPercent = 18m,
            Cet1CostPercent = 12m,
            At1CostPercent = 8m,
            Tier2CostPercent = 5m,
            MaxAt1SharePercent = 30m,
            MaxTier2SharePercent = 35m,
            StepPercent = 0.5m
        });
    }

    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MetadataDbContext(options);
    }
}
