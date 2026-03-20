using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class SubmissionBatchAuditLogConfiguration : IEntityTypeConfiguration<SubmissionBatchAuditLog>
{
    public void Configure(EntityTypeBuilder<SubmissionBatchAuditLog> b)
    {
        b.ToTable("submission_batch_audit_log", "meta");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasColumnType("varchar(40)").IsRequired();
        b.Property(x => x.PerformedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasIndex(x => x.BatchId).HasDatabaseName("IX_submission_batch_audit_log_batch");
        b.HasIndex(x => x.CorrelationId).HasDatabaseName("IX_submission_batch_audit_log_correlation");
        b.HasIndex(x => x.PerformedAt).IsDescending().HasDatabaseName("IX_submission_batch_audit_log_time");
    }
}
