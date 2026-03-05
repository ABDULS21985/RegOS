using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class FilingSlaRecordConfiguration : IEntityTypeConfiguration<FilingSlaRecord>
{
    public void Configure(EntityTypeBuilder<FilingSlaRecord> builder)
    {
        builder.ToTable("filing_sla_records");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.ModuleId).IsRequired();
        builder.Property(s => s.PeriodId).IsRequired();
        builder.Property(s => s.PeriodEndDate).IsRequired();
        builder.Property(s => s.DeadlineDate).IsRequired();

        builder.HasOne(s => s.Module).WithMany().HasForeignKey(s => s.ModuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Period).WithMany().HasForeignKey(s => s.PeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Submission).WithMany().HasForeignKey(s => s.SubmissionId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(s => new { s.TenantId, s.ModuleId, s.PeriodId }).IsUnique();
        builder.HasIndex(s => s.TenantId);
    }
}
