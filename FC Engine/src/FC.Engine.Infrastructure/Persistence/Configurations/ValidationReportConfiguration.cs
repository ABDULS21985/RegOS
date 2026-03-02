using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Persistence.Configurations;

public class ValidationReportConfiguration : IEntityTypeConfiguration<ValidationReport>
{
    public void Configure(EntityTypeBuilder<ValidationReport> builder)
    {
        builder.ToTable("validation_reports");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.SubmissionId).IsRequired();
        builder.Property(e => e.ValidatedAt);

        builder.Ignore(e => e.IsValid);
        builder.Ignore(e => e.HasWarnings);
        builder.Ignore(e => e.HasErrors);
        builder.Ignore(e => e.ErrorCount);
        builder.Ignore(e => e.WarningCount);

        builder.HasMany(e => e.Errors)
            .WithOne(e => e.ValidationReport)
            .HasForeignKey(e => e.ValidationReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ValidationErrorConfiguration : IEntityTypeConfiguration<ValidationError>
{
    public void Configure(EntityTypeBuilder<ValidationError> builder)
    {
        builder.ToTable("validation_errors");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.RuleId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Field).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Message).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.Severity).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ExpectedValue).HasMaxLength(200);
        builder.Property(e => e.ActualValue).HasMaxLength(200);
        builder.Property(e => e.ReferencedReturnCode).HasMaxLength(30);
    }
}
