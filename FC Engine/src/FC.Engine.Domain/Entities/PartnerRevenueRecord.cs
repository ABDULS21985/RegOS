using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class PartnerRevenueRecord
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartnerTenantId { get; set; }
    public int InvoiceId { get; set; }
    public PartnerBillingModel BillingModel { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal? CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal? WholesaleDiscountRate { get; set; }
    public decimal WholesaleDiscountAmount { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Invoice? Invoice { get; set; }
    public Tenant? PartnerTenant { get; set; }
}
