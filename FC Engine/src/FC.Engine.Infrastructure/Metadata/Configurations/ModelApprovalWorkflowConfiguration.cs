using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public sealed class ModelApprovalWorkflowStateRecordConfiguration : IEntityTypeConfiguration<ModelApprovalWorkflowStateRecord>
{
    public void Configure(EntityTypeBuilder<ModelApprovalWorkflowStateRecord> builder)
    {
        builder.ToTable("model_approval_states", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.WorkflowKey).HasMaxLength(240).IsRequired();
        builder.Property(x => x.ModelCode).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ModelName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Artifact).HasMaxLength(240).IsRequired();
        builder.Property(x => x.Stage).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ChangedAtUtc).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.WorkflowKey).IsUnique();
        builder.HasIndex(x => x.Stage);
        builder.HasIndex(x => x.ChangedAtUtc);
    }
}

public sealed class ModelApprovalAuditRecordConfiguration : IEntityTypeConfiguration<ModelApprovalAuditRecord>
{
    public void Configure(EntityTypeBuilder<ModelApprovalAuditRecord> builder)
    {
        builder.ToTable("model_approval_audit", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.WorkflowKey).HasMaxLength(240).IsRequired();
        builder.Property(x => x.ModelCode).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ModelName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Artifact).HasMaxLength(240).IsRequired();
        builder.Property(x => x.PreviousStage).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Stage).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ChangedAtUtc).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.WorkflowKey);
        builder.HasIndex(x => x.Stage);
        builder.HasIndex(x => x.ChangedAtUtc);
    }
}
