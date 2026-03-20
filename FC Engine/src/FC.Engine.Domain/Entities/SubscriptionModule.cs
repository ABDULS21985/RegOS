namespace FC.Engine.Domain.Entities;

public class SubscriptionModule
{
    public int Id { get; set; }
    public int SubscriptionId { get; set; }
    public int ModuleId { get; set; }
    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeactivatedAt { get; set; }
    public decimal PriceMonthly { get; set; }
    public decimal PriceAnnual { get; set; }
    public bool IsActive { get; set; } = true;

    public Subscription? Subscription { get; set; }
    public Module? Module { get; set; }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        DeactivatedAt = DateTime.UtcNow;
    }

    public void Reactivate(decimal priceMonthly, decimal priceAnnual)
    {
        IsActive = true;
        DeactivatedAt = null;
        ActivatedAt = DateTime.UtcNow;
        PriceMonthly = priceMonthly;
        PriceAnnual = priceAnnual;
    }
}
