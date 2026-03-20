using FC.Engine.Domain.Metadata;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FC.Engine.Infrastructure.Tests.Services;

public class FieldLocalisationServiceTests
{
    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MetadataDbContext(options);
    }

    [Fact]
    public async Task GetLocalisations_Returns_Preferred_Language_When_Available()
    {
        using var db = CreateDb();

        var field = new TemplateField
        {
            Id = 101,
            TemplateVersionId = 1,
            FieldName = "institution_name",
            DisplayName = "Institution Name",
            XmlElementName = "InstitutionName",
            FieldOrder = 1,
            DataType = Domain.Enums.FieldDataType.Text,
            SqlType = "nvarchar(255)"
        };
        db.TemplateFields.Add(field);
        db.FieldLocalisations.Add(new FieldLocalisation
        {
            FieldId = field.Id,
            LanguageCode = "fr",
            LocalisedLabel = "Nom de l'institution",
            LocalisedHelpText = "Saisissez le nom officiel"
        });
        await db.SaveChangesAsync();

        var sut = new FieldLocalisationService(db);
        var result = await sut.GetLocalisations(new[] { field.Id }, "fr");

        result.Should().ContainKey(field.Id);
        result[field.Id].Label.Should().Be("Nom de l'institution");
    }

    [Fact]
    public async Task GetLocalisations_Falls_Back_To_English()
    {
        using var db = CreateDb();

        var field = new TemplateField
        {
            Id = 202,
            TemplateVersionId = 1,
            FieldName = "reporting_period",
            DisplayName = "Reporting Period",
            XmlElementName = "ReportingPeriod",
            FieldOrder = 2,
            DataType = Domain.Enums.FieldDataType.Text,
            SqlType = "nvarchar(50)"
        };
        db.TemplateFields.Add(field);
        db.FieldLocalisations.Add(new FieldLocalisation
        {
            FieldId = field.Id,
            LanguageCode = "en",
            LocalisedLabel = "Reporting Period",
            LocalisedHelpText = "Period under review"
        });
        await db.SaveChangesAsync();

        var sut = new FieldLocalisationService(db);
        var result = await sut.GetLocalisations(new[] { field.Id }, "sw");

        result.Should().ContainKey(field.Id);
        result[field.Id].Label.Should().Be("Reporting Period");
        result[field.Id].HelpText.Should().Be("Period under review");
    }
}
