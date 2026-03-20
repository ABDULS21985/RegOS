using FC.Engine.Domain.ValueObjects;
using FC.Engine.Infrastructure.Export;
using FluentAssertions;
using UglyToad.PdfPig;

namespace FC.Engine.Infrastructure.Tests.Export;

public class BoardPackGeneratorTests
{
    [Fact]
    public async Task Generate_Returns_Valid_Pdf_Bytes()
    {
        var sut = new BoardPackGenerator();

        var sections = new List<BoardPackSection>
        {
            new()
            {
                ReportName = "Capital Adequacy Report",
                ColumnNames = ["Institution", "Amount", "Currency"],
                Rows =
                [
                    new Dictionary<string, object?> { ["Institution"] = "Test Bank", ["Amount"] = 1000000m, ["Currency"] = "USD" },
                    new Dictionary<string, object?> { ["Institution"] = "Test CU", ["Amount"] = 500000m, ["Currency"] = "EUR" }
                ]
            }
        };

        var branding = BrandingConfig.WithDefaults();

        var result = await sut.Generate(sections, branding, "Q4 Board Pack");

        result.Should().NotBeEmpty();

        // Verify it's a valid PDF using PdfPig
        using var pdf = PdfDocument.Open(result);
        pdf.NumberOfPages.Should().BeGreaterThanOrEqualTo(3); // cover + TOC + 1 section
    }

    [Fact]
    public async Task Generate_Handles_Multiple_Sections()
    {
        var sut = new BoardPackGenerator();

        var sections = new List<BoardPackSection>
        {
            new()
            {
                ReportName = "Report A",
                ColumnNames = ["Name", "Value"],
                Rows =
                [
                    new Dictionary<string, object?> { ["Name"] = "Row 1", ["Value"] = 100 }
                ]
            },
            new()
            {
                ReportName = "Report B",
                ColumnNames = ["Field1", "Field2"],
                Rows =
                [
                    new Dictionary<string, object?> { ["Field1"] = "Alpha", ["Field2"] = "Beta" },
                    new Dictionary<string, object?> { ["Field1"] = "Gamma", ["Field2"] = "Delta" }
                ]
            },
            new()
            {
                ReportName = "Report C",
                ColumnNames = ["Col1"],
                Rows =
                [
                    new Dictionary<string, object?> { ["Col1"] = "Only row" }
                ]
            }
        };

        var branding = BrandingConfig.WithDefaults();
        var result = await sut.Generate(sections, branding, "Multi-Section Pack");

        result.Should().NotBeEmpty();

        using var pdf = PdfDocument.Open(result);
        pdf.NumberOfPages.Should().BeGreaterThanOrEqualTo(5); // cover + TOC + 3 sections
    }

    [Fact]
    public async Task Generate_Handles_Empty_Section()
    {
        var sut = new BoardPackGenerator();

        var sections = new List<BoardPackSection>
        {
            new()
            {
                ReportName = "Empty Report",
                ColumnNames = [],
                Rows = []
            }
        };

        var branding = BrandingConfig.WithDefaults();
        var result = await sut.Generate(sections, branding, "Empty Pack");

        result.Should().NotBeEmpty();

        using var pdf = PdfDocument.Open(result);
        pdf.NumberOfPages.Should().BeGreaterThanOrEqualTo(3); // cover + TOC + 1 section (shows "no data")
    }

    [Fact]
    public async Task Generate_Uses_Custom_Branding()
    {
        var sut = new BoardPackGenerator();

        var sections = new List<BoardPackSection>
        {
            new()
            {
                ReportName = "Branded Report",
                ColumnNames = ["Name"],
                Rows = [new Dictionary<string, object?> { ["Name"] = "Test" }]
            }
        };

        var branding = BrandingConfig.WithDefaults(new BrandingConfig
        {
            CompanyName = "Acme Financial",
            PrimaryColor = "#1E40AF"
        });

        var result = await sut.Generate(sections, branding, "Branded Pack");

        result.Should().NotBeEmpty();

        // Verify PDF contains the company name
        using var pdf = PdfDocument.Open(result);
        var allText = string.Join(" ", pdf.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
        allText.Should().Contain("Acme");
    }

    [Fact]
    public async Task Generate_Formats_Values_Correctly()
    {
        var sut = new BoardPackGenerator();

        var sections = new List<BoardPackSection>
        {
            new()
            {
                ReportName = "Formatted Values",
                ColumnNames = ["Date", "Amount", "Label"],
                Rows =
                [
                    new Dictionary<string, object?>
                    {
                        ["Date"] = new DateTime(2025, 6, 15),
                        ["Amount"] = 12345.67m,
                        ["Label"] = null
                    }
                ]
            }
        };

        var branding = BrandingConfig.WithDefaults();
        var result = await sut.Generate(sections, branding, "Format Test");

        result.Should().NotBeEmpty();

        using var pdf = PdfDocument.Open(result);
        var allText = string.Join(" ", pdf.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
        allText.Should().Contain("15");
        allText.Should().Contain("Jun");
        allText.Should().Contain("12,345.67");
    }
}
