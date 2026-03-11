using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Tests.Services;

public class SanctionsWatchlistCatalogServiceTests
{
    [Fact]
    public async Task MaterializeAsync_Persists_Deduplicated_Sources_And_Entries_With_Source_Counts()
    {
        await using var db = CreateDb();
        var sut = new SanctionsWatchlistCatalogService(db);

        var request = new SanctionsCatalogMaterializationRequest
        {
            Sources =
            [
                new SanctionsCatalogSourceInput
                {
                    SourceCode = "UN",
                    SourceName = "UN Security Council Consolidated List",
                    RefreshCadence = "Daily",
                    Status = "active"
                },
                new SanctionsCatalogSourceInput
                {
                    SourceCode = "OFAC",
                    SourceName = "OFAC SDN List",
                    RefreshCadence = "Daily",
                    Status = "active"
                },
                new SanctionsCatalogSourceInput
                {
                    SourceCode = "UN",
                    SourceName = "UN Security Council Consolidated List",
                    RefreshCadence = "Daily",
                    Status = "active"
                }
            ],
            Entries =
            [
                new SanctionsCatalogEntryInput
                {
                    SourceCode = "UN",
                    PrimaryName = "AL-QAIDA",
                    Aliases = ["AL QAIDA", "ALQAIDA"],
                    Category = "entity",
                    RiskLevel = "critical"
                },
                new SanctionsCatalogEntryInput
                {
                    SourceCode = "UN",
                    PrimaryName = "AL-QAIDA",
                    Aliases = ["AL QAIDA"],
                    Category = "entity",
                    RiskLevel = "critical"
                },
                new SanctionsCatalogEntryInput
                {
                    SourceCode = "OFAC",
                    PrimaryName = "BOKO HARAM",
                    Aliases = ["JAS"],
                    Category = "entity",
                    RiskLevel = "critical"
                }
            ]
        };

        var result = await sut.MaterializeAsync(request);

        result.SourceCount.Should().Be(2);
        result.EntryCount.Should().Be(2);
        result.Sources.Should().ContainSingle(x => x.SourceCode == "UN" && x.EntryCount == 1);
        result.Sources.Should().ContainSingle(x => x.SourceCode == "OFAC" && x.EntryCount == 1);

        var persistedSources = await db.SanctionsCatalogSources.AsNoTracking().ToListAsync();
        var persistedEntries = await db.SanctionsCatalogEntries.AsNoTracking().ToListAsync();

        persistedSources.Should().HaveCount(2);
        persistedEntries.Should().HaveCount(2);
        persistedSources.Should().Contain(x => x.SourceCode == "UN" && x.EntryCount == 1);
        persistedEntries.Should().Contain(x => x.EntryKey == "UN:ALQAIDA");

        var state = await sut.LoadAsync();
        state.Sources.Should().ContainSingle(x => x.SourceCode == "UN" && x.EntryCount == 1);
        state.Entries.Should().ContainSingle(x =>
            x.SourceCode == "UN"
            && x.PrimaryName == "AL-QAIDA"
            && x.Aliases.Contains("AL QAIDA")
            && x.Aliases.Contains("ALQAIDA"));
    }

    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MetadataDbContext(options);
    }
}
