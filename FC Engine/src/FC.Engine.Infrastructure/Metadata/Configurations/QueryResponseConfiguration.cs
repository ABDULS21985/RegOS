using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class QueryResponseConfiguration : IEntityTypeConfiguration<QueryResponse>
{
    public void Configure(EntityTypeBuilder<QueryResponse> b)
    {
        b.ToTable("query_responses", "meta");
        b.HasKey(x => x.Id);
        b.Property(x => x.RegulatorAckRef).HasColumnType("varchar(80)");
        b.Property(x => x.SubmittedAt).HasColumnType("datetime2(3)");
        b.Property(x => x.CreatedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.HasMany(x => x.Attachments).WithOne(a => a.QueryResponse)
            .HasForeignKey(a => a.QueryResponseId).OnDelete(DeleteBehavior.Cascade);
    }
}
