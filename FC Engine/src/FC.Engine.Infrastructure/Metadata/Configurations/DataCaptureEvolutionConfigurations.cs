using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class ReturnLockConfiguration : IEntityTypeConfiguration<ReturnLock>
{
    public void Configure(EntityTypeBuilder<ReturnLock> builder)
    {
        builder.ToTable("return_locks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.UserName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LockedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.HeartbeatAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.SubmissionId).IsUnique();
        builder.HasIndex(x => x.ExpiresAt);
    }
}

public class DataFeedRequestLogConfiguration : IEntityTypeConfiguration<DataFeedRequestLog>
{
    public void Configure(EntityTypeBuilder<DataFeedRequestLog> builder)
    {
        builder.ToTable("data_feed_request_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.ReturnCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(150).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResultJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
    }
}

public class TenantFieldMappingConfiguration : IEntityTypeConfiguration<TenantFieldMapping>
{
    public void Configure(EntityTypeBuilder<TenantFieldMapping> builder)
    {
        builder.ToTable("tenant_field_mappings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.IntegrationName).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ReturnCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ExternalFieldName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TemplateFieldName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.IntegrationName, x.ReturnCode, x.ExternalFieldName }).IsUnique();
    }
}
