using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Persistence.Configurations;

public class InstitutionConfiguration : IEntityTypeConfiguration<Institution>
{
    public void Configure(EntityTypeBuilder<Institution> builder)
    {
        builder.ToTable("institutions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.InstitutionCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.InstitutionName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.LicenseType).HasMaxLength(50);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(e => e.InstitutionCode).IsUnique();
    }
}
