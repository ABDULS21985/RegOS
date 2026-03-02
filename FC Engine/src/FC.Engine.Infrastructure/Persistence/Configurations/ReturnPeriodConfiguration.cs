using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Persistence.Configurations;

public class ReturnPeriodConfiguration : IEntityTypeConfiguration<ReturnPeriod>
{
    public void Configure(EntityTypeBuilder<ReturnPeriod> builder)
    {
        builder.ToTable("return_periods");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.Year).IsRequired();
        builder.Property(e => e.Month).IsRequired();
        builder.Property(e => e.Frequency).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(e => new { e.Year, e.Month, e.Frequency }).IsUnique();
    }
}
