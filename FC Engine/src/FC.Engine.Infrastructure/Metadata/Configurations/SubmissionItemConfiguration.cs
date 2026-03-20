using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class SubmissionItemConfiguration : IEntityTypeConfiguration<SubmissionItem>
{
    public void Configure(EntityTypeBuilder<SubmissionItem> b)
    {
        b.ToTable("submission_items", "meta");
        b.HasKey(x => x.Id);
        b.Property(x => x.ReturnCode).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.ExportFormat).HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.ExportPayloadHash).HasColumnType("varchar(128)").IsRequired();
        b.Property(x => x.RegulatorCode).HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.RegulatorReference).HasColumnType("varchar(80)");
        b.Property(x => x.ReportingPeriod).HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.Status).HasColumnType("varchar(30)").HasDefaultValue("PENDING");
        b.Property(x => x.CreatedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");

        // Idempotency: same submission + regulator + version cannot be re-submitted
        b.HasIndex(new[] { "SubmissionId", "RegulatorCode", "ReturnVersion" })
            .IsUnique()
            .HasDatabaseName("UQ_submission_items_idempotent");
        b.HasIndex(x => x.SubmissionId).HasDatabaseName("IX_submission_items_submission");

        b.HasMany(x => x.Signatures).WithOne(s => s.SubmissionItem)
            .HasForeignKey(s => s.SubmissionItemId).OnDelete(DeleteBehavior.Cascade);
    }
}
