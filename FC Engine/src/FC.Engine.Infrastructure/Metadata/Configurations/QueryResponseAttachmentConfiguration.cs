using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class QueryResponseAttachmentConfiguration : IEntityTypeConfiguration<QueryResponseAttachment>
{
    public void Configure(EntityTypeBuilder<QueryResponseAttachment> b)
    {
        b.ToTable("query_response_attachments", "meta");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.ContentType).HasColumnType("varchar(100)").IsRequired();
        b.Property(x => x.BlobStoragePath).HasMaxLength(500).IsRequired();
        b.Property(x => x.FileHash).HasColumnType("varchar(128)").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
