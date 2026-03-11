using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public sealed class SanctionsScreeningRunRecordConfiguration : IEntityTypeConfiguration<SanctionsScreeningRunRecord>
{
    public void Configure(EntityTypeBuilder<SanctionsScreeningRunRecord> builder)
    {
        builder.ToTable("sanctions_screening_runs", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ScreeningKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ThresholdPercent).IsRequired();
        builder.Property(x => x.ScreenedAt).IsRequired();
        builder.Property(x => x.TotalSubjects).IsRequired();
        builder.Property(x => x.MatchCount).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.ScreeningKey).IsUnique();
        builder.HasIndex(x => x.ScreenedAt);
    }
}

public sealed class SanctionsScreeningResultRecordConfiguration : IEntityTypeConfiguration<SanctionsScreeningResultRecord>
{
    public void Configure(EntityTypeBuilder<SanctionsScreeningResultRecord> builder)
    {
        builder.ToTable("sanctions_screening_results", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ScreeningKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(240).IsRequired();
        builder.Property(x => x.Disposition).HasMaxLength(40).IsRequired();
        builder.Property(x => x.MatchScore).IsRequired();
        builder.Property(x => x.MatchedName).HasMaxLength(240).IsRequired();
        builder.Property(x => x.SourceCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.SourceName).HasMaxLength(240).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(40).IsRequired();
        builder.Property(x => x.RiskLevel).HasMaxLength(30).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.ScreeningKey);
        builder.HasIndex(x => x.Subject);
    }
}

public sealed class SanctionsTransactionCheckRecordConfiguration : IEntityTypeConfiguration<SanctionsTransactionCheckRecord>
{
    public void Configure(EntityTypeBuilder<SanctionsTransactionCheckRecord> builder)
    {
        builder.ToTable("sanctions_transaction_checks", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TransactionKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.TransactionReference).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Channel).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ThresholdPercent).IsRequired();
        builder.Property(x => x.ControlDecision).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Narrative).HasMaxLength(1200).IsRequired();
        builder.Property(x => x.ScreenedAt).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.TransactionKey).IsUnique();
        builder.HasIndex(x => x.ScreenedAt);
    }
}

public sealed class SanctionsTransactionPartyResultRecordConfiguration : IEntityTypeConfiguration<SanctionsTransactionPartyResultRecord>
{
    public void Configure(EntityTypeBuilder<SanctionsTransactionPartyResultRecord> builder)
    {
        builder.ToTable("sanctions_transaction_party_results", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TransactionKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.PartyRole).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PartyName).HasMaxLength(240).IsRequired();
        builder.Property(x => x.Disposition).HasMaxLength(40).IsRequired();
        builder.Property(x => x.MatchScore).IsRequired();
        builder.Property(x => x.MatchedName).HasMaxLength(240).IsRequired();
        builder.Property(x => x.SourceCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.RiskLevel).HasMaxLength(30).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.TransactionKey);
        builder.HasIndex(x => x.PartyName);
    }
}
