using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class ChsScoreSnapshotConfiguration : IEntityTypeConfiguration<ChsScoreSnapshot>
{
    public void Configure(EntityTypeBuilder<ChsScoreSnapshot> builder)
    {
        builder.ToTable("chs_score_snapshots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.PeriodLabel).HasMaxLength(20).IsRequired();
        builder.Property(s => s.ComputedAt).IsRequired();
        builder.Property(s => s.OverallScore).HasPrecision(5, 2).IsRequired();
        builder.Property(s => s.Rating).IsRequired();
        builder.Property(s => s.FilingTimeliness).HasPrecision(5, 2);
        builder.Property(s => s.DataQuality).HasPrecision(5, 2);
        builder.Property(s => s.RegulatoryCapital).HasPrecision(5, 2);
        builder.Property(s => s.AuditGovernance).HasPrecision(5, 2);
        builder.Property(s => s.Engagement).HasPrecision(5, 2);

        builder.HasIndex(s => new { s.TenantId, s.PeriodLabel }).IsUnique();
        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.ComputedAt);
    }
}
