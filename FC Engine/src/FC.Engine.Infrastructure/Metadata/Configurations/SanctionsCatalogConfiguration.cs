using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public sealed class SanctionsCatalogSourceRecordConfiguration : IEntityTypeConfiguration<SanctionsCatalogSourceRecord>
{
    public void Configure(EntityTypeBuilder<SanctionsCatalogSourceRecord> builder)
    {
        builder.ToTable("sanctions_watchlist_sources", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.SourceName).HasMaxLength(240).IsRequired();
        builder.Property(x => x.RefreshCadence).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.EntryCount).IsRequired();
        builder.Property(x => x.MaterializedAt).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.SourceCode).IsUnique();
        builder.HasIndex(x => x.MaterializedAt);
        builder.HasIndex(x => x.Status);
    }
}

public sealed class SanctionsCatalogEntryRecordConfiguration : IEntityTypeConfiguration<SanctionsCatalogEntryRecord>
{
    public void Configure(EntityTypeBuilder<SanctionsCatalogEntryRecord> builder)
    {
        builder.ToTable("sanctions_watchlist_entries", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntryKey).HasMaxLength(240).IsRequired();
        builder.Property(x => x.SourceCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PrimaryName).HasMaxLength(240).IsRequired();
        builder.Property(x => x.AliasesJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.Category).HasMaxLength(40).IsRequired();
        builder.Property(x => x.RiskLevel).HasMaxLength(30).IsRequired();
        builder.Property(x => x.MaterializedAt).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.EntryKey).IsUnique();
        builder.HasIndex(x => x.SourceCode);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.MaterializedAt);
    }
}
