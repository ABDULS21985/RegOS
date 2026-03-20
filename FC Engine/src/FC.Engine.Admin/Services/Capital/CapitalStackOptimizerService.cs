using FC.Engine.Infrastructure.Services;

namespace FC.Engine.Admin.Services.Capital;

public sealed class CapitalStackOptimizerService
{
    private readonly CapitalPlanningScenarioStoreService _scenarioStore;

    public CapitalStackOptimizerService(CapitalPlanningScenarioStoreService scenarioStore)
    {
        _scenarioStore = scenarioStore;
    }

    public async Task<StackOptimizationResult> OptimizeAsync(
        StackOptimizationRequest request,
        CancellationToken ct = default)
    {
        var scenario = await _scenarioStore.LoadAsync(ct);

        var targetCar = request.TargetCarPercent > 0 ? request.TargetCarPercent : scenario?.TargetCarPercent ?? 15m;
        var currentRwa = request.CurrentRwaBn > 0 ? request.CurrentRwaBn : scenario?.CurrentRwaBn ?? 100m;
        var maxAt1Share = request.MaxAt1SharePercent > 0 ? request.MaxAt1SharePercent : scenario?.MaxAt1SharePercent ?? 30m;
        var maxTier2Share = request.MaxTier2SharePercent > 0 ? request.MaxTier2SharePercent : scenario?.MaxTier2SharePercent ?? 35m;

        var cet1Cost = request.Cet1CostPercent > 0 ? request.Cet1CostPercent : scenario?.Cet1CostPercent ?? 12m;
        var at1Cost = request.At1CostPercent > 0 ? request.At1CostPercent : scenario?.At1CostPercent ?? 8m;
        var tier2Cost = request.Tier2CostPercent > 0 ? request.Tier2CostPercent : scenario?.Tier2CostPercent ?? 5m;

        // Required total capital to meet target CAR
        var requiredCapitalBn = currentRwa * targetCar / 100m;

        // Optimize: maximize cheaper instruments within regulatory constraints
        // AT1 can be at most maxAt1Share% of total capital
        // Tier 2 can be at most maxTier2Share% of total capital
        // CET1 must be at least (100 - maxAt1Share - maxTier2Share)% of total capital

        var maxAt1Bn = requiredCapitalBn * maxAt1Share / 100m;
        var maxTier2Bn = requiredCapitalBn * maxTier2Share / 100m;
        var minCet1Pct = 100m - maxAt1Share - maxTier2Share;
        if (minCet1Pct < 0) minCet1Pct = 0;
        var minCet1Bn = requiredCapitalBn * minCet1Pct / 100m;

        // Basel minimum: CET1 >= 4.5% of RWA, Tier 1 >= 6% of RWA, Total >= 8% of RWA
        var baselMinCet1 = currentRwa * 4.5m / 100m;
        var baselMinTier1 = currentRwa * 6m / 100m;

        // Effective CET1 floor: max of constraint-based and Basel-based
        var effectiveCet1Bn = Math.Max(minCet1Bn, baselMinCet1);

        // Tier 1 = CET1 + AT1; ensure AT1 doesn't push CET1 below Basel minimum
        var effectiveAt1Bn = Math.Min(maxAt1Bn, requiredCapitalBn - effectiveCet1Bn - 0m);
        if (effectiveAt1Bn < 0) effectiveAt1Bn = 0;

        // Ensure Tier1 >= baselMinTier1
        var tier1 = effectiveCet1Bn + effectiveAt1Bn;
        if (tier1 < baselMinTier1)
        {
            effectiveCet1Bn = baselMinTier1 - effectiveAt1Bn;
            if (effectiveCet1Bn < baselMinCet1) effectiveCet1Bn = baselMinCet1;
        }

        // Tier 2 fills the remainder
        var effectiveTier2Bn = requiredCapitalBn - effectiveCet1Bn - effectiveAt1Bn;
        if (effectiveTier2Bn < 0) effectiveTier2Bn = 0;
        if (effectiveTier2Bn > maxTier2Bn)
        {
            // Excess must go to CET1
            effectiveCet1Bn += (effectiveTier2Bn - maxTier2Bn);
            effectiveTier2Bn = maxTier2Bn;
        }

        // Blended cost of capital
        var totalCapital = effectiveCet1Bn + effectiveAt1Bn + effectiveTier2Bn;
        var blendedCost = totalCapital > 0
            ? (effectiveCet1Bn * cet1Cost + effectiveAt1Bn * at1Cost + effectiveTier2Bn * tier2Cost) / totalCapital
            : 0;

        // Compare with CET1-only cost
        var cet1OnlyCost = cet1Cost;
        var costSavingBps = (int)Math.Round((cet1OnlyCost - blendedCost) * 100m);

        return new StackOptimizationResult
        {
            TargetCarPercent = targetCar,
            CurrentRwaBn = currentRwa,
            RequiredCapitalBn = Math.Round(requiredCapitalBn, 2),
            Cet1Bn = Math.Round(effectiveCet1Bn, 2),
            Cet1SharePercent = totalCapital > 0 ? Math.Round(effectiveCet1Bn / totalCapital * 100m, 1) : 100m,
            At1Bn = Math.Round(effectiveAt1Bn, 2),
            At1SharePercent = totalCapital > 0 ? Math.Round(effectiveAt1Bn / totalCapital * 100m, 1) : 0,
            Tier2Bn = Math.Round(effectiveTier2Bn, 2),
            Tier2SharePercent = totalCapital > 0 ? Math.Round(effectiveTier2Bn / totalCapital * 100m, 1) : 0,
            BlendedCostPercent = Math.Round(blendedCost, 2),
            Cet1OnlyCostPercent = cet1OnlyCost,
            CostSavingBps = costSavingBps,
            Cet1CostPercent = cet1Cost,
            At1CostPercent = at1Cost,
            Tier2CostPercent = tier2Cost,
            MaxAt1SharePercent = maxAt1Share,
            MaxTier2SharePercent = maxTier2Share,
            ScenarioSavedAt = scenario?.SavedAtUtc
        };
    }

    public async Task<List<CostFrontierPoint>> GetCostFrontierAsync(
        StackOptimizationRequest request,
        CancellationToken ct = default)
    {
        var scenario = await _scenarioStore.LoadAsync(ct);

        var currentRwa = request.CurrentRwaBn > 0 ? request.CurrentRwaBn : scenario?.CurrentRwaBn ?? 100m;
        var cet1Cost = request.Cet1CostPercent > 0 ? request.Cet1CostPercent : scenario?.Cet1CostPercent ?? 12m;
        var at1Cost = request.At1CostPercent > 0 ? request.At1CostPercent : scenario?.At1CostPercent ?? 8m;
        var tier2Cost = request.Tier2CostPercent > 0 ? request.Tier2CostPercent : scenario?.Tier2CostPercent ?? 5m;
        var maxAt1 = request.MaxAt1SharePercent > 0 ? request.MaxAt1SharePercent : scenario?.MaxAt1SharePercent ?? 30m;
        var maxTier2 = request.MaxTier2SharePercent > 0 ? request.MaxTier2SharePercent : scenario?.MaxTier2SharePercent ?? 35m;

        var points = new List<CostFrontierPoint>();

        // Generate frontier for CAR from 8% to 25% in 0.5% steps
        for (var car = 8.0m; car <= 25.0m; car += 0.5m)
        {
            var requiredCapital = currentRwa * car / 100m;

            // Optimal mix at this CAR level
            var optAt1 = Math.Min(requiredCapital * maxAt1 / 100m, requiredCapital * 0.5m);
            var optTier2 = Math.Min(requiredCapital * maxTier2 / 100m, requiredCapital - optAt1);
            if (optTier2 < 0) optTier2 = 0;
            var optCet1 = requiredCapital - optAt1 - optTier2;
            if (optCet1 < 0) { optCet1 = 0; optTier2 = requiredCapital - optAt1; }

            var total = optCet1 + optAt1 + optTier2;
            var blended = total > 0
                ? (optCet1 * cet1Cost + optAt1 * at1Cost + optTier2 * tier2Cost) / total
                : cet1Cost;

            points.Add(new CostFrontierPoint
            {
                CarPercent = car,
                RequiredCapitalBn = Math.Round(requiredCapital, 2),
                BlendedCostPercent = Math.Round(blended, 2),
                Cet1OnlyCostPercent = cet1Cost,
                Cet1SharePercent = total > 0 ? Math.Round(optCet1 / total * 100m, 1) : 100m,
                At1SharePercent = total > 0 ? Math.Round(optAt1 / total * 100m, 1) : 0,
                Tier2SharePercent = total > 0 ? Math.Round(optTier2 / total * 100m, 1) : 0
            });
        }

        return points;
    }
}

public sealed class StackOptimizationRequest
{
    public decimal TargetCarPercent { get; init; }
    public decimal CurrentRwaBn { get; init; }
    public decimal Cet1CostPercent { get; init; }
    public decimal At1CostPercent { get; init; }
    public decimal Tier2CostPercent { get; init; }
    public decimal MaxAt1SharePercent { get; init; }
    public decimal MaxTier2SharePercent { get; init; }
}

public sealed class StackOptimizationResult
{
    public decimal TargetCarPercent { get; init; }
    public decimal CurrentRwaBn { get; init; }
    public decimal RequiredCapitalBn { get; init; }
    public decimal Cet1Bn { get; init; }
    public decimal Cet1SharePercent { get; init; }
    public decimal At1Bn { get; init; }
    public decimal At1SharePercent { get; init; }
    public decimal Tier2Bn { get; init; }
    public decimal Tier2SharePercent { get; init; }
    public decimal BlendedCostPercent { get; init; }
    public decimal Cet1OnlyCostPercent { get; init; }
    public int CostSavingBps { get; init; }
    public decimal Cet1CostPercent { get; init; }
    public decimal At1CostPercent { get; init; }
    public decimal Tier2CostPercent { get; init; }
    public decimal MaxAt1SharePercent { get; init; }
    public decimal MaxTier2SharePercent { get; init; }
    public DateTime? ScenarioSavedAt { get; init; }
}

public sealed class CostFrontierPoint
{
    public decimal CarPercent { get; init; }
    public decimal RequiredCapitalBn { get; init; }
    public decimal BlendedCostPercent { get; init; }
    public decimal Cet1OnlyCostPercent { get; init; }
    public decimal Cet1SharePercent { get; init; }
    public decimal At1SharePercent { get; init; }
    public decimal Tier2SharePercent { get; init; }
}
