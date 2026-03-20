using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("import_jobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.TemplateId).IsRequired();
        builder.Property(x => x.InstitutionId).IsRequired();
        builder.Property(x => x.SourceFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.SourceFormat).HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).HasConversion<string>().HasDefaultValue(Domain.Enums.ImportJobStatus.Uploaded).IsRequired();
        builder.Property(x => x.StagedData).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ValidationReport).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ImportedBy).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(x => x.Template)
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.TemplateId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
    }
}

public class ImportMappingConfiguration : IEntityTypeConfiguration<ImportMapping>
{
    public void Configure(EntityTypeBuilder<ImportMapping> builder)
    {
        builder.ToTable("import_mappings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.InstitutionId).IsRequired();
        builder.Property(x => x.TemplateId).IsRequired();
        builder.Property(x => x.SourceFormat).HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(x => x.SourceIdentifier).HasMaxLength(200);
        builder.Property(x => x.MappingConfig).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(x => x.Template)
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.InstitutionId, x.TemplateId, x.SourceFormat }).IsUnique();
    }
}

public class MigrationModuleSignOffConfiguration : IEntityTypeConfiguration<MigrationModuleSignOff>
{
    public void Configure(EntityTypeBuilder<MigrationModuleSignOff> builder)
    {
        builder.ToTable("migration_module_signoffs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.ModuleId).IsRequired();
        builder.Property(x => x.IsSignedOff).IsRequired();
        builder.Property(x => x.SignedOffBy).IsRequired();
        builder.Property(x => x.SignedOffAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.ModuleId }).IsUnique();
    }
}
