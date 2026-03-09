using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class SubmissionBatchConfiguration : IEntityTypeConfiguration<SubmissionBatch>
{
    public void Configure(EntityTypeBuilder<SubmissionBatch> b)
    {
        b.ToTable("submission_batches", "meta");
        b.HasKey(x => x.Id);
        b.Property(x => x.BatchReference).HasColumnType("varchar(60)").IsRequired();
        b.Property(x => x.RegulatorCode).HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.Status).HasColumnType("varchar(30)").HasDefaultValue("PENDING");
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.Property(x => x.SubmittedAt).HasColumnType("datetime2(3)");
        b.Property(x => x.AcknowledgedAt).HasColumnType("datetime2(3)");
        b.Property(x => x.FinalStatusAt).HasColumnType("datetime2(3)");
        b.Property(x => x.CreatedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(x => x.BatchReference).IsUnique().HasDatabaseName("UQ_submission_batches_ref");
        b.HasIndex(new[] { "InstitutionId", "Status" }).HasDatabaseName("IX_submission_batches_institution_status");
        b.HasIndex(new[] { "RegulatorCode", "Status" }).HasDatabaseName("IX_submission_batches_regulator");

        b.HasOne(x => x.Channel)
            .WithMany()
            .HasForeignKey(x => x.ChannelId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Items).WithOne(i => i.Batch).HasForeignKey(i => i.BatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Receipts).WithOne(r => r.Batch).HasForeignKey(r => r.BatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Queries).WithOne(q => q.Batch).HasForeignKey(q => q.BatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.AuditLogs).WithOne().HasForeignKey(a => a.BatchId).OnDelete(DeleteBehavior.NoAction);
    }
}
