using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public sealed class DashboardBriefingPackSectionRecordConfiguration : IEntityTypeConfiguration<DashboardBriefingPackSectionRecord>
{
    public void Configure(EntityTypeBuilder<DashboardBriefingPackSectionRecord> builder)
    {
        builder.ToTable("dashboard_briefing_pack_sections", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Lens).HasMaxLength(40).IsRequired();
        builder.Property(x => x.SectionCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.SectionName).HasMaxLength(240).IsRequired();
        builder.Property(x => x.Coverage).HasMaxLength(600).IsRequired();
        builder.Property(x => x.Signal).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Commentary).HasMaxLength(1200).IsRequired();
        builder.Property(x => x.RecommendedAction).HasMaxLength(1200).IsRequired();
        builder.Property(x => x.MaterializedAt).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => new { x.Lens, x.InstitutionId, x.SectionCode }).IsUnique();
        builder.HasIndex(x => x.MaterializedAt);
        builder.HasIndex(x => new { x.Lens, x.InstitutionId });
    }
}
