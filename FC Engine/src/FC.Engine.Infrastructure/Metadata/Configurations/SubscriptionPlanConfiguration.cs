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
        builder.Property(e => e.PlanName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.MaxInstitutions).HasDefaultValue(1);
        builder.Property(e => e.MaxUsersPerEntity).HasDefaultValue(10);
        builder.Property(e => e.MaxModules).HasDefaultValue(1);
        builder.Property(e => e.AllModulesIncluded).HasDefaultValue(false);
        builder.Property(e => e.Features).HasMaxLength(500).HasDefaultValue("xml_submission,validation,reporting");
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.DisplayOrder).HasDefaultValue(0);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(e => e.PlanCode).IsUnique();
    }
}
