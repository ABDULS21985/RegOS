using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public sealed class PlatformIntelligenceRefreshRunRecordConfiguration : IEntityTypeConfiguration<PlatformIntelligenceRefreshRunRecord>
{
    public void Configure(EntityTypeBuilder<PlatformIntelligenceRefreshRunRecord> builder)
    {
        builder.ToTable("platform_intelligence_refresh_runs", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StartedAtUtc).IsRequired();
        builder.Property(x => x.CompletedAtUtc).IsRequired();
        builder.Property(x => x.Succeeded).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.FailureMessage).HasMaxLength(1200);
        builder.Property(x => x.GeneratedAtUtc);
        builder.Property(x => x.DurationMilliseconds).IsRequired();
        builder.Property(x => x.InstitutionCount).IsRequired();
        builder.Property(x => x.InterventionCount).IsRequired();
        builder.Property(x => x.TimelineCount).IsRequired();
        builder.Property(x => x.DashboardPacksMaterialized).IsRequired();
        builder.Property(x => x.RolloutCatalogMaterializedAt);
        builder.Property(x => x.KnowledgeCatalogMaterializedAt);
        builder.Property(x => x.KnowledgeDossierMaterializedAt);
        builder.Property(x => x.CapitalPackMaterializedAt);
        builder.Property(x => x.SanctionsPackMaterializedAt);
        builder.Property(x => x.SanctionsStrDraftCatalogMaterializedAt);
        builder.Property(x => x.ResiliencePackMaterializedAt);
        builder.Property(x => x.ModelRiskPackMaterializedAt);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.CompletedAtUtc);
        builder.HasIndex(x => x.Succeeded);
        builder.HasIndex(x => x.Status);
    }
}
