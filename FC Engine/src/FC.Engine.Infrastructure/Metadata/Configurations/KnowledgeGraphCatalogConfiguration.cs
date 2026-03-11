using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public sealed class KnowledgeGraphNodeConfiguration : IEntityTypeConfiguration<KnowledgeGraphNode>
{
    public void Configure(EntityTypeBuilder<KnowledgeGraphNode> builder)
    {
        builder.ToTable("kg_nodes", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NodeKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NodeType).HasMaxLength(60).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(240).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(120);
        builder.Property(x => x.RegulatorCode).HasMaxLength(40);
        builder.Property(x => x.SourceReference).HasMaxLength(160);
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.MaterializedAt).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.NodeKey).IsUnique();
        builder.HasIndex(x => x.NodeType);
        builder.HasIndex(x => x.RegulatorCode);
    }
}

public sealed class KnowledgeGraphEdgeConfiguration : IEntityTypeConfiguration<KnowledgeGraphEdge>
{
    public void Configure(EntityTypeBuilder<KnowledgeGraphEdge> builder)
    {
        builder.ToTable("kg_edges", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EdgeKey).HasMaxLength(320).IsRequired();
        builder.Property(x => x.EdgeType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.SourceNodeKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TargetNodeKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RegulatorCode).HasMaxLength(40);
        builder.Property(x => x.SourceReference).HasMaxLength(160);
        builder.Property(x => x.Weight).HasDefaultValue(1);
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.MaterializedAt).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.EdgeKey).IsUnique();
        builder.HasIndex(x => x.EdgeType);
        builder.HasIndex(x => x.SourceNodeKey);
        builder.HasIndex(x => x.TargetNodeKey);
        builder.HasIndex(x => x.RegulatorCode);
    }
}
