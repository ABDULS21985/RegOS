namespace FC.Engine.Domain.Entities;

public class PlanModulePricing
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public int ModuleId { get; set; }
    public decimal PriceMonthly { get; set; }
    public decimal PriceAnnual { get; set; }
    public bool IsIncludedInBase { get; set; }

    public SubscriptionPlan? Plan { get; set; }
    public Module? Module { get; set; }
}
