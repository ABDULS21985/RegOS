using FC.Engine.Admin.Services.Capital;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FC.Engine.Admin.Tests.Services;

public class RwaOptimizationServiceTests
{
    [Fact]
    public async Task GetCompositionAsync_Returns_Empty_When_No_Scenario_Exists()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);

        var result = await sut.GetCompositionAsync();

        result.TotalRwaBn.Should().Be(0);
        result.Should().BeSameAs(RwaCompositionData.Empty);
    }

    [Fact]
    public async Task GetCompositionAsync_Returns_Composition_Based_On_Scenario_And_Catalog()
    {
        await using var db = CreateDb();
        await SeedScenario(db, currentCar: 15m, currentRwa: 100m);
        await SeedCatalog(db,
            ("COLLATERAL", "RWA", rwaOpt: 4.5m, capAction: 0m, earningsDelta: 0m, cost: 0.9m),
            ("REBALANCE", "RWA", rwaOpt: 6.5m, capAction: 0m, earningsDelta: -0.3m, cost: 1.2m));

        var sut = CreateSut(db);
        var result = await sut.GetCompositionAsync();

        result.TotalRwaBn.Should().Be(100m);
        result.CreditRiskPct.Should().Be(70m);
        result.MarketRiskPct.Should().Be(15m);
        result.OperationalRiskPct.Should().Be(15m);
        result.TotalOptimizationPotentialPct.Should().Be(11m); // 4.5 + 6.5
        result.CurrentCarPercent.Should().Be(15m);
        result.OptimizedCarPercent.Should().BeGreaterThan(15m);
    }

    [Fact]
    public async Task GetSuggestionsAsync_Returns_Empty_When_No_Scenario()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);

        var result = await sut.GetSuggestionsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSuggestionsAsync_Returns_Actions_Ordered_By_Car_Improvement()
    {
        await using var db = CreateDb();
        await SeedScenario(db, currentCar: 15m, currentRwa: 100m);
        await SeedCatalog(db,
            ("COLLATERAL", "RWA", rwaOpt: 4.5m, capAction: 0m, earningsDelta: 0m, cost: 0.9m),
            ("REBALANCE", "RWA", rwaOpt: 6.5m, capAction: 0m, earningsDelta: -0.3m, cost: 1.2m),
            ("ISSUANCE", "Capital", rwaOpt: 0m, capAction: 10m, earningsDelta: 0m, cost: 14.5m));

        var sut = CreateSut(db);
        var result = await sut.GetSuggestionsAsync();

        // ISSUANCE has 0% RWA opt so it's excluded (filter is RwaOpt > 0 OR lever == RWA)
        // REBALANCE has higher CAR improvement than COLLATERAL
        result.Should().HaveCount(2);
        result[0].ActionCode.Should().Be("REBALANCE");
        result[0].CarImprovementBps.Should().BeGreaterThan(result[1].CarImprovementBps);
        result[1].ActionCode.Should().Be("COLLATERAL");
    }

    [Fact]
    public async Task SimulateActionAsync_Returns_NoScenario_When_No_Scenario_Exists()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);

        var result = await sut.SimulateActionAsync("COLLATERAL", 1.0m);

        result.Should().BeSameAs(RwaWhatIfResult.NoScenario);
    }

    [Fact]
    public async Task SimulateActionAsync_Returns_NoScenario_When_Action_Not_Found()
    {
        await using var db = CreateDb();
        await SeedScenario(db, currentCar: 15m, currentRwa: 100m);
        await SeedCatalog(db, ("COLLATERAL", "RWA", rwaOpt: 4.5m, capAction: 0m, earningsDelta: 0m, cost: 0.9m));

        var sut = CreateSut(db);
        var result = await sut.SimulateActionAsync("NONEXISTENT", 1.0m);

        result.Should().BeSameAs(RwaWhatIfResult.NoScenario);
    }

    [Fact]
    public async Task SimulateActionAsync_Projects_8_Quarters_With_Action_Applied_In_Q1()
    {
        await using var db = CreateDb();
        await SeedScenario(db, currentCar: 15m, currentRwa: 100m, rwaGrowth: 2m, earnings: 0.5m);
        await SeedCatalog(db, ("COLLATERAL", "RWA", rwaOpt: 4.5m, capAction: 0m, earningsDelta: 0m, cost: 0.9m));

        var sut = CreateSut(db);
        var result = await sut.SimulateActionAsync("COLLATERAL", 1.0m);

        result.ActionCode.Should().Be("COLLATERAL");
        result.BaselineCarPercent.Should().Be(15m);
        result.ProjectedCarPercent.Should().BeGreaterThan(15m);
        result.RwaReductionBn.Should().BeGreaterThan(0);
        result.QuarterProjections.Should().HaveCount(9); // Current + Q1-Q8
        result.QuarterProjections[0].Label.Should().Be("Current");
        result.QuarterProjections[1].Label.Should().Be("Q1");
    }

    [Fact]
    public async Task SimulateActionAsync_Respects_Magnitude_Multiplier()
    {
        await using var db = CreateDb();
        await SeedScenario(db, currentCar: 15m, currentRwa: 100m, rwaGrowth: 2m, earnings: 0.5m);
        await SeedCatalog(db, ("COLLATERAL", "RWA", rwaOpt: 4.5m, capAction: 0m, earningsDelta: 0m, cost: 0.9m));

        var sut = CreateSut(db);
        var result1x = await sut.SimulateActionAsync("COLLATERAL", 1.0m);
        var result2x = await sut.SimulateActionAsync("COLLATERAL", 2.0m);

        result2x.RwaReductionBn.Should().BeGreaterThan(result1x.RwaReductionBn);
        result2x.ProjectedCarPercent.Should().BeGreaterThan(result1x.ProjectedCarPercent);
    }

    private static RwaOptimizationService CreateSut(MetadataDbContext db) =>
        new(new CapitalPlanningScenarioStoreService(db), new CapitalActionCatalogService(db));

    private static async Task SeedScenario(
        MetadataDbContext db,
        decimal currentCar = 15m,
        decimal currentRwa = 100m,
        decimal rwaGrowth = 2m,
        decimal earnings = 0.5m)
    {
        var store = new CapitalPlanningScenarioStoreService(db);
        await store.SaveAsync(new CapitalPlanningScenarioCommand
        {
            CurrentCarPercent = currentCar,
            CurrentRwaBn = currentRwa,
            QuarterlyRwaGrowthPercent = rwaGrowth,
            QuarterlyRetainedEarningsBn = earnings,
            MinimumRequirementPercent = 10m,
            ConservationBufferPercent = 2.5m,
            CountercyclicalBufferPercent = 0.5m,
            DsibBufferPercent = 1m,
            TargetCarPercent = 15m,
            Cet1CostPercent = 12m,
            At1CostPercent = 8m,
            Tier2CostPercent = 5m,
            MaxAt1SharePercent = 30m,
            MaxTier2SharePercent = 35m,
            StepPercent = 0.5m
        });
    }

    private static async Task SeedCatalog(
        MetadataDbContext db,
        params (string code, string lever, decimal rwaOpt, decimal capAction, decimal earningsDelta, decimal cost)[] templates)
    {
        var catalog = new CapitalActionCatalogService(db);
        await catalog.MaterializeAsync(templates.Select(t => new CapitalActionTemplateInput
        {
            Code = t.code,
            Title = $"{t.code} action",
            Summary = $"Test {t.code} action",
            PrimaryLever = t.lever,
            RwaOptimisationPercent = t.rwaOpt,
            CapitalActionBn = t.capAction,
            QuarterlyRetainedEarningsDeltaBn = t.earningsDelta,
            EstimatedAnnualCostPercent = t.cost
        }).ToList());
    }

    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MetadataDbContext(options);
    }
}
