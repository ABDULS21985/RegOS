using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("subscription_plans");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.PlanCode).HasMaxLength(30).IsRequired();
        builder.Property(e => e.PlanName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.Tier).HasDefaultValue(0);
        builder.Property(e => e.MaxModules).HasDefaultValue(1);
        builder.Property(e => e.MaxUsersPerEntity).HasDefaultValue(10);
        builder.Property(e => e.MaxEntities).HasDefaultValue(1);
        builder.Property(e => e.MaxApiCallsPerMonth).HasDefaultValue(0);
        builder.Property(e => e.MaxStorageMb).HasDefaultValue(500);
        builder.Property(e => e.BasePriceMonthly).HasColumnType("decimal(18,2)");
        builder.Property(e => e.BasePriceAnnual).HasColumnType("decimal(18,2)");
        builder.Property(e => e.TrialDays).HasDefaultValue(14);
        builder.Property(e => e.Features).HasColumnType("nvarchar(max)");
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.DisplayOrder).HasDefaultValue(0);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(e => e.PlanCode).IsUnique();

        builder.HasMany(e => e.ModulePricing)
            .WithOne(mp => mp.Plan)
            .HasForeignKey(mp => mp.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Subscriptions)
            .WithOne(s => s.Plan)
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
