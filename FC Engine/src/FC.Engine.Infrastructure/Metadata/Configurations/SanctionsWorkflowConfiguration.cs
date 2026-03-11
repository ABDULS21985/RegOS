using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public sealed class SanctionsFalsePositiveEntryConfiguration : IEntityTypeConfiguration<SanctionsFalsePositiveEntry>
{
    public void Configure(EntityTypeBuilder<SanctionsFalsePositiveEntry> builder)
    {
        builder.ToTable("sanctions_false_positive_library", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MatchKey).HasMaxLength(240).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(240).IsRequired();
        builder.Property(x => x.MatchedName).HasMaxLength(240).IsRequired();
        builder.Property(x => x.SourceCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.RiskLevel).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReviewedAtUtc).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.MatchKey).IsUnique();
        builder.HasIndex(x => x.ReviewedAtUtc);
        builder.HasIndex(x => x.SourceCode);
    }
}

public sealed class SanctionsDecisionAuditRecordConfiguration : IEntityTypeConfiguration<SanctionsDecisionAuditRecord>
{
    public void Configure(EntityTypeBuilder<SanctionsDecisionAuditRecord> builder)
    {
        builder.ToTable("sanctions_decision_audit", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MatchKey).HasMaxLength(240).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(240).IsRequired();
        builder.Property(x => x.MatchedName).HasMaxLength(240).IsRequired();
        builder.Property(x => x.SourceCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PreviousDecision).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Decision).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ReviewedAtUtc).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.MatchKey);
        builder.HasIndex(x => x.ReviewedAtUtc);
        builder.HasIndex(x => x.Decision);
    }
}
