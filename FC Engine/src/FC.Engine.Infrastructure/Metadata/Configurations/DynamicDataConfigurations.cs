using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class SubmissionFieldSourceConfiguration : IEntityTypeConfiguration<SubmissionFieldSource>
{
    public void Configure(EntityTypeBuilder<SubmissionFieldSource> builder)
    {
        builder.ToTable("submission_field_sources", "meta");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReturnCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FieldName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DataSource).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SourceDetail).HasMaxLength(500);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.ReturnCode, x.SubmissionId, x.FieldName })
            .IsUnique();
    }
}
