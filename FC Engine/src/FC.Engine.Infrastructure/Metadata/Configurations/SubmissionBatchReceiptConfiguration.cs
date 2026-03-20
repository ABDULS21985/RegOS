using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class SubmissionBatchReceiptConfiguration : IEntityTypeConfiguration<SubmissionBatchReceipt>
{
    public void Configure(EntityTypeBuilder<SubmissionBatchReceipt> b)
    {
        b.ToTable("submission_batch_receipts", "meta");
        b.HasKey(x => x.Id);
        b.Property(x => x.RegulatorCode).HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.ReceiptReference).HasColumnType("varchar(120)").IsRequired();
        b.Property(x => x.ReceiptTimestamp).HasColumnType("datetime2(3)");
        b.Property(x => x.ReceivedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasIndex(x => x.ReceiptReference).HasDatabaseName("IX_submission_batch_receipts_ref");
    }
}
