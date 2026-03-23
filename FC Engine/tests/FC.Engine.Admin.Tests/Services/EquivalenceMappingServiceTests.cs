using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Models;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services.CrossBorder;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FC.Engine.Admin.Tests.Services;

public class EquivalenceMappingServiceTests
{
    private static MetadataDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new MetadataDbContext(options);
    }

    private static EquivalenceMappingService CreateSut(MetadataDbContext db) =>
        new(db, Mock.Of<IHarmonisationAuditLogger>(), NullLogger<EquivalenceMappingService>.Instance);

    private static List<EquivalenceEntryInput> CreateTestEntries() =>
    [
        new()
        {
            JurisdictionCode = "NG", RegulatorCode = "CBN",
            LocalParameterCode = "CAR_MIN", LocalParameterName = "Minimum CAR",
            LocalThreshold = 15.0m, ThresholdUnit = "PERCENTAGE",
            CalculationBasis = "Total RWA", RegulatoryFramework = "BASEL_III"
        },
        new()
        {
            JurisdictionCode = "GH", RegulatorCode = "BOG",
            LocalParameterCode = "CAR_MIN", LocalParameterName = "Minimum CAR",
            LocalThreshold = 13.0m, ThresholdUnit = "PERCENTAGE",
            CalculationBasis = "Total RWA", RegulatoryFramework = "BASEL_III"
        }
    ];

    [Fact]
    public async Task CreateMappingAsync_Persists_Mapping_And_Entries()
    {
        var db = CreateDb(nameof(CreateMappingAsync_Persists_Mapping_And_Entries));
        var sut = CreateSut(db);

        var id = await sut.CreateMappingAsync(
            "CAR_MINIMUM", "Minimum Capital Adequacy Ratio", "CAPITAL_ADEQUACY",
            "Cross-border CAR comparison", CreateTestEntries(), userId: 42);

        id.Should().BeGreaterThan(0);

        var detail = await sut.GetMappingAsync(id);
        detail.Should().NotBeNull();
        detail!.MappingCode.Should().Be("CAR_MINIMUM");
        detail.MappingName.Should().Be("Minimum Capital Adequacy Ratio");
        detail.ConceptDomain.Should().Be("CAPITAL_ADEQUACY");
        detail.Description.Should().Be("Cross-border CAR comparison");
        detail.Entries.Should().HaveCount(2);

        var ngEntry = detail.Entries.First(e => e.JurisdictionCode == "NG");
        ngEntry.LocalThreshold.Should().Be(15.0m);
        ngEntry.RegulatoryFramework.Should().Be("BASEL_III");

        var ghEntry = detail.Entries.First(e => e.JurisdictionCode == "GH");
        ghEntry.LocalThreshold.Should().Be(13.0m);
    }

    [Fact]
    public async Task GetMappingAsync_Returns_Null_For_NonExistent_Id()
    {
        var db = CreateDb(nameof(GetMappingAsync_Returns_Null_For_NonExistent_Id));
        var sut = CreateSut(db);

        var result = await sut.GetMappingAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListMappingsAsync_Filters_By_Domain()
    {
        var db = CreateDb(nameof(ListMappingsAsync_Filters_By_Domain));
        var sut = CreateSut(db);

        await sut.CreateMappingAsync("CAR_MIN", "CAR Minimum", "CAPITAL_ADEQUACY", null, CreateTestEntries(), 1);
        await sut.CreateMappingAsync("LCR_MIN", "LCR Minimum", "LIQUIDITY", null, CreateTestEntries(), 1);

        var allMappings = await sut.ListMappingsAsync(null);
        allMappings.Should().HaveCount(2);

        var capitalOnly = await sut.ListMappingsAsync("CAPITAL_ADEQUACY");
        capitalOnly.Should().HaveCount(1);
        capitalOnly[0].MappingCode.Should().Be("CAR_MIN");
    }

    [Fact]
    public async Task AddEntryAsync_Appends_Entry_To_Existing_Mapping()
    {
        var db = CreateDb(nameof(AddEntryAsync_Appends_Entry_To_Existing_Mapping));
        var sut = CreateSut(db);

        var id = await sut.CreateMappingAsync("TEST", "Test", "LEVERAGE", null, CreateTestEntries(), 1);

        await sut.AddEntryAsync(id, new EquivalenceEntryInput
        {
            JurisdictionCode = "KE", RegulatorCode = "CBK",
            LocalParameterCode = "CAR_MIN", LocalParameterName = "Minimum CAR",
            LocalThreshold = 14.5m, ThresholdUnit = "PERCENTAGE",
            CalculationBasis = "Total RWA", RegulatoryFramework = "BASEL_III"
        }, 1);

        var detail = await sut.GetMappingAsync(id);
        detail!.Entries.Should().HaveCount(3);
        detail.Entries.Should().ContainSingle(e => e.JurisdictionCode == "KE");
    }

    [Fact]
    public async Task UpdateThresholdAsync_Changes_Threshold_Value()
    {
        var db = CreateDb(nameof(UpdateThresholdAsync_Changes_Threshold_Value));
        var sut = CreateSut(db);

        var id = await sut.CreateMappingAsync("UPD_TEST", "Update Test", "CAPITAL_ADEQUACY", null, CreateTestEntries(), 1);

        await sut.UpdateThresholdAsync(id, "NG", 16.5m, 1);

        var detail = await sut.GetMappingAsync(id);
        detail!.Entries.First(e => e.JurisdictionCode == "NG").LocalThreshold.Should().Be(16.5m);
    }

    [Fact]
    public async Task UpdateThresholdAsync_Throws_For_NonExistent_Entry()
    {
        var db = CreateDb(nameof(UpdateThresholdAsync_Throws_For_NonExistent_Entry));
        var sut = CreateSut(db);

        var id = await sut.CreateMappingAsync("ERR_TEST", "Error Test", "FX", null, CreateTestEntries(), 1);

        var act = () => sut.UpdateThresholdAsync(id, "ZZ", 10m, 1);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task GetCrossBorderComparisonAsync_Returns_Sorted_Thresholds()
    {
        var db = CreateDb(nameof(GetCrossBorderComparisonAsync_Returns_Sorted_Thresholds));
        var sut = CreateSut(db);

        await sut.CreateMappingAsync("CMP_TEST", "Comparison", "CAPITAL_ADEQUACY", null, CreateTestEntries(), 1);

        var thresholds = await sut.GetCrossBorderComparisonAsync("CMP_TEST");
        thresholds.Should().HaveCount(2);
        thresholds[0].JurisdictionCode.Should().Be("GH");
        thresholds[1].JurisdictionCode.Should().Be("NG");
    }

    [Fact]
    public async Task GetCrossBorderComparisonAsync_Throws_For_NonExistent_Code()
    {
        var db = CreateDb(nameof(GetCrossBorderComparisonAsync_Throws_For_NonExistent_Code));
        var sut = CreateSut(db);

        var act = () => sut.GetCrossBorderComparisonAsync("NONEXISTENT");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Full_Roundtrip_Create_AddEntry_UpdateThreshold_List_Compare()
    {
        var db = CreateDb(nameof(Full_Roundtrip_Create_AddEntry_UpdateThreshold_List_Compare));
        var sut = CreateSut(db);

        // Create
        var id = await sut.CreateMappingAsync(
            "ROUNDTRIP", "Roundtrip Test", "CAPITAL_ADEQUACY", "End-to-end test",
            CreateTestEntries(), userId: 7);

        // Read
        var mapping = await sut.GetMappingAsync(id);
        mapping.Should().NotBeNull();
        mapping!.Entries.Should().HaveCount(2);

        // Add entry
        await sut.AddEntryAsync(id, new EquivalenceEntryInput
        {
            JurisdictionCode = "ZA", RegulatorCode = "SARB",
            LocalParameterCode = "CAR_MIN", LocalParameterName = "Minimum CAR",
            LocalThreshold = 11.5m, ThresholdUnit = "PERCENTAGE",
            CalculationBasis = "Total RWA", RegulatoryFramework = "BASEL_III"
        }, 7);

        // Update threshold
        await sut.UpdateThresholdAsync(id, "GH", 14.0m, 7);

        // Verify final state
        var final = await sut.GetMappingAsync(id);
        final!.Entries.Should().HaveCount(3);
        final.Entries.First(e => e.JurisdictionCode == "GH").LocalThreshold.Should().Be(14.0m);
        final.Entries.Should().ContainSingle(e => e.JurisdictionCode == "ZA");

        // Verify comparison
        var comparison = await sut.GetCrossBorderComparisonAsync("ROUNDTRIP");
        comparison.Should().HaveCount(3);

        // Verify list
        var list = await sut.ListMappingsAsync("CAPITAL_ADEQUACY");
        list.Should().ContainSingle(m => m.MappingCode == "ROUNDTRIP");
        list[0].JurisdictionCount.Should().Be(3);
    }
}
