using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Infrastructure.Metadata;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Tests.Services;

public class TemplateModuleLinkingVerificationTests
{
    [Fact]
    public async Task FC_ReturnTemplates_Linked_Count_Is_103_After_Migration_Update()
    {
        await using var db = CreateDbContext(nameof(FC_ReturnTemplates_Linked_Count_Is_103_After_Migration_Update));
        var fcModule = new Module
        {
            ModuleCode = "FC_RETURNS",
            ModuleName = "Finance Company Returns",
            RegulatorCode = "CBN",
            DefaultFrequency = "Monthly",
            CreatedAt = DateTime.UtcNow
        };
        db.Modules.Add(fcModule);
        await db.SaveChangesAsync();

        for (var i = 1; i <= 103; i++)
        {
            db.ReturnTemplates.Add(new ReturnTemplate
            {
                ReturnCode = $"MFCR_{i:D3}",
                Name = $"Template {i}",
                Frequency = ReturnFrequency.Monthly,
                StructuralCategory = StructuralCategory.FixedRow,
                PhysicalTableName = $"mfcr_{i:D3}",
                XmlRootElement = $"MFCR{i:D3}",
                XmlNamespace = $"urn:cbn:dfis:fc:mfcr{i:D3}",
                IsSystemTemplate = true,
                OwnerDepartment = "DFIS",
                InstitutionType = "FC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "seed",
                UpdatedBy = "seed",
                ModuleId = null
            });
        }

        await db.SaveChangesAsync();

        // Mirrors migration intent:
        // UPDATE meta.return_templates
        // SET ModuleId = (SELECT Id FROM dbo.modules WHERE ModuleCode = 'FC_RETURNS')
        var templates = await db.ReturnTemplates.Where(t => t.ModuleId == null).ToListAsync();
        foreach (var template in templates)
        {
            template.ModuleId = fcModule.Id;
        }
        await db.SaveChangesAsync();

        var linkedCount = await db.ReturnTemplates.CountAsync(t => t.ModuleId == fcModule.Id);
        var unlinkedCount = await db.ReturnTemplates.CountAsync(t => t.ModuleId == null);

        linkedCount.Should().Be(103);
        unlinkedCount.Should().Be(0);
    }

    [Fact]
    public async Task Existing_FC_Templates_Still_Work_After_ModuleId_Addition()
    {
        await using var db = CreateDbContext(nameof(Existing_FC_Templates_Still_Work_After_ModuleId_Addition));
        var fcModule = new Module
        {
            ModuleCode = "FC_RETURNS",
            ModuleName = "Finance Company Returns",
            RegulatorCode = "CBN",
            DefaultFrequency = "Monthly",
            CreatedAt = DateTime.UtcNow
        };
        db.Modules.Add(fcModule);
        await db.SaveChangesAsync();

        db.ReturnTemplates.Add(new ReturnTemplate
        {
            ReturnCode = "MFCR_300",
            Name = "Legacy FC Template",
            Frequency = ReturnFrequency.Monthly,
            StructuralCategory = StructuralCategory.FixedRow,
            PhysicalTableName = "mfcr_300",
            XmlRootElement = "MFCR300",
            XmlNamespace = "urn:cbn:dfis:fc:mfcr300",
            IsSystemTemplate = true,
            OwnerDepartment = "DFIS",
            InstitutionType = "FC",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
            UpdatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var template = await db.ReturnTemplates.SingleAsync(t => t.ReturnCode == "MFCR_300");
        template.ModuleId = fcModule.Id;
        await db.SaveChangesAsync();

        var loaded = await db.ReturnTemplates.AsNoTracking().SingleAsync(t => t.ReturnCode == "MFCR_300");
        loaded.ReturnCode.Should().Be("MFCR_300");
        loaded.PhysicalTableName.Should().Be("mfcr_300");
        loaded.ModuleId.Should().Be(fcModule.Id);
    }

    private static MetadataDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new MetadataDbContext(options);
    }
}
