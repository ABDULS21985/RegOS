using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Tests.Services;

public class SanctionsWorkflowStoreServiceTests
{
    [Fact]
    public async Task RecordDecisionAsync_Persists_Audit_And_FalsePositive_Memory()
    {
        await using var db = CreateDb();
        var sut = new SanctionsWorkflowStoreService(db);

        await sut.RecordDecisionAsync(new SanctionsWorkflowDecisionCommand
        {
            MatchKey = "BOKOHARAM|OFAC|BOKOHARAM",
            Subject = "Boko Haram",
            MatchedName = "Boko Haram",
            SourceCode = "OFAC",
            RiskLevel = "critical",
            PreviousDecision = "Review",
            Decision = "False Positive",
            ReviewedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        });

        var afterFalsePositive = await sut.LoadAsync();
        afterFalsePositive.FalsePositiveLibrary.Should().ContainSingle();
        afterFalsePositive.AuditTrail.Should().ContainSingle();
        afterFalsePositive.LatestDecisions.Should().ContainSingle();
        afterFalsePositive.LatestDecisions[0].Decision.Should().Be("False Positive");

        await sut.RecordDecisionAsync(new SanctionsWorkflowDecisionCommand
        {
            MatchKey = "BOKOHARAM|OFAC|BOKOHARAM",
            Subject = "Boko Haram",
            MatchedName = "Boko Haram",
            SourceCode = "OFAC",
            RiskLevel = "critical",
            PreviousDecision = "False Positive",
            Decision = "Confirm Match",
            ReviewedAtUtc = DateTime.UtcNow
        });

        var finalState = await sut.LoadAsync();
        finalState.FalsePositiveLibrary.Should().BeEmpty();
        finalState.AuditTrail.Should().HaveCount(2);
        finalState.LatestDecisions.Should().ContainSingle();
        finalState.LatestDecisions[0].Decision.Should().Be("Confirm Match");

        var auditRecords = await db.SanctionsDecisionAuditRecords.AsNoTracking().ToListAsync();
        auditRecords.Should().HaveCount(2);

        var falsePositiveRecords = await db.SanctionsFalsePositiveEntries.AsNoTracking().ToListAsync();
        falsePositiveRecords.Should().BeEmpty();
    }

    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MetadataDbContext(options);
    }
}
