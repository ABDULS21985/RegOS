using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Metadata.Configurations;

public sealed class ResilienceAssessmentResponseRecordConfiguration : IEntityTypeConfiguration<ResilienceAssessmentResponseRecord>
{
    public void Configure(EntityTypeBuilder<ResilienceAssessmentResponseRecord> builder)
    {
        builder.ToTable("resilience_self_assessment_responses", "meta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuestionId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Domain).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Prompt).HasMaxLength(600).IsRequired();
        builder.Property(x => x.Score).IsRequired();
        builder.Property(x => x.AnsweredAtUtc).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.QuestionId).IsUnique();
        builder.HasIndex(x => x.Domain);
        builder.HasIndex(x => x.AnsweredAtUtc);
    }
}
