using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Tests.Services;

public class OpsResiliencePackCatalogServiceTests
{
    [Fact]
    public async Task MaterializeAsync_Persists_Pack_Sheets_And_Loads_Them_Back()
    {
        await using var db = CreateDb();
        var sut = new OpsResiliencePackCatalogService(db);

        var pack = new List<OpsResiliencePackSheetInput>
        {
            new()
            {
                SheetCode = "OPS-01",
                SheetName = "Important Business Services Inventory",
                RowCount = 12,
                Signal = "Current",
                Coverage = "12 service row(s).",
                Commentary = "Coverage is current.",
                RecommendedAction = "Maintain service ownership mapping."
            },
            new()
            {
                SheetCode = "OPS-02",
                SheetName = "Impact Tolerance Definitions",
                RowCount = 8,
                Signal = "Watch",
                Coverage = "8 tolerance row(s).",
                Commentary = "Dependency concentration is elevated.",
                RecommendedAction = "Challenge declared tolerances."
            }
        };

        var materialized = await sut.MaterializeAsync(pack);
        var loaded = await sut.LoadAsync();

        materialized.Sheets.Should().HaveCount(2);
        materialized.Sheets.Should().ContainSingle(x => x.SheetCode == "OPS-02" && x.Signal == "Watch");
        loaded.Sheets.Should().HaveCount(2);
        loaded.Sheets.Should().ContainSingle(x => x.SheetCode == "OPS-01" && x.RowCount == 12);

        var persisted = await db.OpsResiliencePackSheets.AsNoTracking().ToListAsync();
        persisted.Should().HaveCount(2);
        persisted.Should().Contain(x => x.SheetCode == "OPS-02" && x.Signal == "Watch");
    }

    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MetadataDbContext(options);
    }
}
