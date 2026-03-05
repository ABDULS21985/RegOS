using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class PlanModulePricingConfiguration : IEntityTypeConfiguration<PlanModulePricing>
{
    public void Configure(EntityTypeBuilder<PlanModulePricing> builder)
    {
        builder.ToTable("plan_module_pricing");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.PriceMonthly).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.PriceAnnual).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.IsIncludedInBase).HasDefaultValue(false);

        builder.HasOne(e => e.Plan)
            .WithMany(p => p.ModulePricing)
            .HasForeignKey(e => e.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Module)
            .WithMany(m => m.PlanModulePricing)
            .HasForeignKey(e => e.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.PlanId, e.ModuleId }).IsUnique();
    }
}

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.Status)
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(SubscriptionStatus.Trial);
        builder.Property(e => e.BillingFrequency)
            .HasMaxLength(10)
            .HasConversion<string>()
            .HasDefaultValue(BillingFrequency.Monthly);
        builder.Property(e => e.CancellationReason).HasMaxLength(500);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(e => e.Tenant)
            .WithMany(t => t.Subscriptions)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Plan)
            .WithMany(p => p.Subscriptions)
            .HasForeignKey(e => e.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Modules)
            .WithOne(sm => sm.Subscription)
            .HasForeignKey(sm => sm.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Invoices)
            .WithOne(i => i.Subscription)
            .HasForeignKey(i => i.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.Status });
    }
}

public class SubscriptionModuleConfiguration : IEntityTypeConfiguration<SubscriptionModule>
{
    public void Configure(EntityTypeBuilder<SubscriptionModule> builder)
    {
        builder.ToTable("subscription_modules");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.PriceMonthly).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.PriceAnnual).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.ActivatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(e => e.Subscription)
            .WithMany(s => s.Modules)
            .HasForeignKey(e => e.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Module)
            .WithMany(m => m.SubscriptionModules)
            .HasForeignKey(e => e.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.SubscriptionId, e.ModuleId }).IsUnique();
        builder.HasIndex(e => new { e.SubscriptionId, e.IsActive });
    }
}

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Subtotal).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.VatRate).HasColumnType("decimal(5,4)").HasDefaultValue(0.0750m);
        builder.Property(e => e.VatAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("NGN");
        builder.Property(e => e.Status)
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(InvoiceStatus.Draft);
        builder.Property(e => e.VoidReason).HasMaxLength(500);
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(e => e.Subscription)
            .WithMany(s => s.Invoices)
            .HasForeignKey(e => e.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.LineItems)
            .WithOne(li => li.Invoice)
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.InvoiceNumber).IsUnique();
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.Status, e.DueDate });
    }
}

public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("invoice_line_items");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.LineType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.LineTotal).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Quantity).HasDefaultValue(1);
        builder.Property(e => e.DisplayOrder).HasDefaultValue(0);

        builder.HasOne(e => e.Invoice)
            .WithMany(i => i.LineItems)
            .HasForeignKey(e => e.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Module)
            .WithMany()
            .HasForeignKey(e => e.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.InvoiceId, e.DisplayOrder });
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(3).HasDefaultValue("NGN");
        builder.Property(e => e.PaymentMethod).HasMaxLength(30).IsRequired();
        builder.Property(e => e.PaymentReference).HasMaxLength(100);
        builder.Property(e => e.ProviderTransactionId).HasMaxLength(100);
        builder.Property(e => e.ProviderName).HasMaxLength(30);
        builder.Property(e => e.Status)
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(PaymentStatus.Pending);
        builder.Property(e => e.FailureReason).HasMaxLength(500);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(e => e.Invoice)
            .WithMany()
            .HasForeignKey(e => e.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.InvoiceId);
        builder.HasIndex(e => e.PaymentReference);
    }
}

public class UsageRecordConfiguration : IEntityTypeConfiguration<UsageRecord>
{
    public void Configure(EntityTypeBuilder<UsageRecord> builder)
    {
        builder.ToTable("usage_records");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.RecordDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.StorageUsedMb).HasColumnType("decimal(18,2)").HasDefaultValue(0m);

        builder.HasIndex(e => new { e.TenantId, e.RecordDate }).IsUnique();
        builder.HasIndex(e => e.TenantId);
    }
}
