using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Persistence.Configurations;

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("return_submissions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.InstitutionId).IsRequired();
        builder.Property(e => e.ReturnPeriodId).IsRequired();
        builder.Property(e => e.ReturnCodeValue).HasColumnName("return_code").HasMaxLength(30).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.SubmittedAt);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(e => e.Institution)
            .WithMany()
            .HasForeignKey(e => e.InstitutionId);

        builder.HasOne(e => e.ReturnPeriod)
            .WithMany()
            .HasForeignKey(e => e.ReturnPeriodId);

        builder.HasOne(e => e.ValidationReport)
            .WithOne(v => v.Submission)
            .HasForeignKey<ValidationReport>(v => v.SubmissionId);

        builder.HasIndex(e => new { e.InstitutionId, e.ReturnPeriodId, e.ReturnCodeValue });
    }
}
