using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class RegulatoryChannelConfiguration : IEntityTypeConfiguration<RegulatoryChannel>
{
    public void Configure(EntityTypeBuilder<RegulatoryChannel> b)
    {
        b.ToTable("regulatory_channels", "meta");
        b.HasKey(x => x.Id);
        b.Property(x => x.RegulatorCode).HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.RegulatorName).HasMaxLength(120).IsRequired();
        b.Property(x => x.PortalName).HasMaxLength(120).IsRequired();
        b.Property(x => x.IntegrationMethod).HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.BaseUrl).HasMaxLength(500);
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.RequiresCertificate).HasDefaultValue(true);
        b.Property(x => x.TimeoutSeconds).HasDefaultValue(120);
        b.Property(x => x.CreatedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasIndex(x => x.RegulatorCode).IsUnique().HasDatabaseName("UQ_regulatory_channels_code");
    }
}
