using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class SubmissionSignatureRecordConfiguration : IEntityTypeConfiguration<SubmissionSignatureRecord>
{
    public void Configure(EntityTypeBuilder<SubmissionSignatureRecord> b)
    {
        b.ToTable("submission_signatures", "meta");
        b.HasKey(x => x.Id);
        b.Property(x => x.CertificateThumbprint).HasColumnType("varchar(64)").IsRequired();
        b.Property(x => x.SignatureAlgorithm).HasColumnType("varchar(30)").IsRequired();
        b.Property(x => x.SignatureValue).HasColumnType("varbinary(max)").IsRequired();
        b.Property(x => x.SignedDataHash).HasColumnType("varchar(128)").IsRequired();
        b.Property(x => x.SignedAt).HasColumnType("datetime2(3)");
        b.Property(x => x.IsValid).HasDefaultValue(true);
        b.HasIndex(x => x.CertificateThumbprint).HasDatabaseName("IX_submission_signatures_cert");
    }
}
