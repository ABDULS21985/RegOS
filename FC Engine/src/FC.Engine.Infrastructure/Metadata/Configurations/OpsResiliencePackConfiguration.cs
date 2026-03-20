using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public sealed class OpsResiliencePackSheetRecordConfiguration : IEntityTypeConfiguration<OpsResiliencePackSheetRecord>
{
    public void Configure(EntityTypeBuilder<OpsResiliencePackSheetRecord> builder)
    {
        builder.ToTable("ops_resilience_pack_sheets", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SheetCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.SheetName).HasMaxLength(240).IsRequired();
        builder.Property(x => x.RowCount).IsRequired();
        builder.Property(x => x.Signal).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Coverage).HasMaxLength(600).IsRequired();
        builder.Property(x => x.Commentary).HasMaxLength(1200).IsRequired();
        builder.Property(x => x.RecommendedAction).HasMaxLength(1200).IsRequired();
        builder.Property(x => x.MaterializedAt).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.SheetCode).IsUnique();
        builder.HasIndex(x => x.Signal);
        builder.HasIndex(x => x.MaterializedAt);
    }
}
