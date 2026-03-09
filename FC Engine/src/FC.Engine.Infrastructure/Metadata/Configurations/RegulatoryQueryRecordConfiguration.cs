using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public class RegulatoryQueryRecordConfiguration : IEntityTypeConfiguration<RegulatoryQueryRecord>
{
    public void Configure(EntityTypeBuilder<RegulatoryQueryRecord> b)
    {
        b.ToTable("regulatory_query_records", "meta");
        b.HasKey(x => x.Id);
        b.Property(x => x.RegulatorCode).HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.QueryReference).HasColumnType("varchar(80)").IsRequired();
        b.Property(x => x.QueryType).HasColumnType("varchar(30)").HasDefaultValue("CLARIFICATION");
        b.Property(x => x.Priority).HasColumnType("varchar(10)").HasDefaultValue("NORMAL");
        b.Property(x => x.Status).HasColumnType("varchar(20)").HasDefaultValue("OPEN");
        b.Property(x => x.ReceivedAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RespondedAt).HasColumnType("datetime2(3)");
        b.Property(x => x.ClosedAt).HasColumnType("datetime2(3)");

        b.HasIndex(new[] { "InstitutionId", "Status" }).HasDatabaseName("IX_regulatory_query_records_institution");
        b.HasIndex(new[] { "DueDate", "Status" }).HasDatabaseName("IX_regulatory_query_records_due");

        b.HasMany(x => x.Responses).WithOne(r => r.Query)
            .HasForeignKey(r => r.QueryId).OnDelete(DeleteBehavior.Cascade);
    }
}
