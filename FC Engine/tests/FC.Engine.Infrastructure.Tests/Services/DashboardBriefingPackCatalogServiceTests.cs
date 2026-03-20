using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Tests.Services;

public class DashboardBriefingPackCatalogServiceTests
{
    [Fact]
    public async Task MaterializeAsync_Persists_Pack_Per_Lens_And_Institution()
    {
        await using var db = CreateDb();
        var sut = new DashboardBriefingPackCatalogService(db);

        var sections = new List<DashboardBriefingPackSectionInput>
        {
            new()
            {
                SectionCode = "GOV-01",
                SectionName = "Systemic Posture",
                Coverage = "3 critical intervention(s)",
                Signal = "Critical",
                Commentary = "Population-wide pressure remains elevated.",
                RecommendedAction = "Escalate the strategic agenda."
            },
            new()
            {
                SectionCode = "GOV-02",
                SectionName = "Compliance & Filing",
                Coverage = "11 escalated obligation(s)",
                Signal = "Watch",
                Commentary = "Overdue filings remain in the queue.",
                RecommendedAction = "Drive corrective filing recovery."
            }
        };

        var materialized = await sut.MaterializeAsync("governor", null, sections);
        var loaded = await sut.LoadAsync("governor", null);

        materialized.Sections.Should().HaveCount(2);
        loaded.Sections.Should().ContainSingle(x => x.SectionCode == "GOV-01" && x.Signal == "Critical");

        var executiveSections = new List<DashboardBriefingPackSectionInput>
        {
            new()
            {
                SectionCode = "EXE-01",
                SectionName = "Filing Posture",
                Coverage = "1 overdue | 2 due soon",
                Signal = "Watch",
                Commentary = "Institution filing pressure remains active.",
                RecommendedAction = "Resolve the next deadline."
            }
        };

        await sut.MaterializeAsync("executive", 11, executiveSections);
        var executiveLoaded = await sut.LoadAsync("executive", 11);

        executiveLoaded.Sections.Should().ContainSingle(x => x.SectionCode == "EXE-01" && x.InstitutionId == 11);

        var persisted = await db.DashboardBriefingPackSections.AsNoTracking().ToListAsync();
        persisted.Should().HaveCount(3);
    }

    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MetadataDbContext(options);
    }
}
