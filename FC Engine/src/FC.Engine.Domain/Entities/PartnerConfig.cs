using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class PartnerConfig
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public PartnerTier PartnerTier { get; set; } = PartnerTier.Silver;
    public PartnerBillingModel BillingModel { get; set; } = PartnerBillingModel.Direct;
    public decimal? CommissionRate { get; set; }
    public decimal? WholesaleDiscount { get; set; }
    public int MaxSubTenants { get; set; } = 10;
    public DateTime? AgreementSignedAt { get; set; }
    public string? AgreementVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
}
