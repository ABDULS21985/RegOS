using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Tests.Services;

public class ModelRiskPackCatalogServiceTests
{
    [Fact]
    public async Task MaterializeAsync_Persists_Model_Risk_Pack_Sheets_And_Loads_Them_Back()
    {
        await using var db = CreateDb();
        var sut = new ModelRiskPackCatalogService(db);

        var pack = new List<ModelRiskPackSheetInput>
        {
            new()
            {
                SheetCode = "MRM-01",
                SheetName = "Model Inventory Summary",
                RowCount = 6,
                Signal = "Current",
                Coverage = "6 model row(s).",
                Commentary = "Inventory baseline is complete.",
                RecommendedAction = "Maintain inventory hygiene."
            },
            new()
            {
                SheetCode = "MRM-02",
                SheetName = "Validation Status",
                RowCount = 4,
                Signal = "Watch",
                Coverage = "4 validation row(s).",
                Commentary = "Two validations are approaching deadline.",
                RecommendedAction = "Refresh the next validation cycle."
            }
        };

        var materialized = await sut.MaterializeAsync(pack);
        var loaded = await sut.LoadAsync();

        materialized.Sheets.Should().HaveCount(2);
        materialized.Sheets.Should().ContainSingle(x => x.SheetCode == "MRM-02" && x.Signal == "Watch");
        loaded.Sheets.Should().HaveCount(2);
        loaded.Sheets.Should().ContainSingle(x => x.SheetCode == "MRM-01" && x.RowCount == 6);

        var persisted = await db.ModelRiskPackSheets.AsNoTracking().ToListAsync();
        persisted.Should().HaveCount(2);
        persisted.Should().Contain(x => x.SheetCode == "MRM-02" && x.Signal == "Watch");
    }

    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MetadataDbContext(options);
    }
}
