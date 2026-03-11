using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Infrastructure.DynamicSchema;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FC.Engine.Infrastructure.Tests.Services;

public class OpsResilienceModuleLoadingTests
{
    [Fact]
    public async Task Ops_Resilience_Module_Imports_And_Publishes_Ten_Return_Sheets()
    {
        await using var db = CreateDbContext(nameof(Ops_Resilience_Module_Imports_And_Publishes_Ten_Return_Sheets));
        var module = await SeedModules(db, "OPS_RESILIENCE");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out var ddlExecutor);

        var definition = await LoadDefinition("ops-resilience-module-definition.json");
        var validation = await sut.ValidateDefinition(definition);
        validation.IsValid.Should().BeTrue(string.Join(" | ", validation.Errors));
        validation.TemplateCount.Should().Be(10);

        var import = await sut.ImportModule(definition, "ops-resilience-test");
        import.Success.Should().BeTrue(string.Join(" | ", import.Errors));
        import.TemplatesCreated.Should().Be(10);
        import.CrossSheetRulesCreated.Should().Be(3);
        import.FormulasCreated.Should().Be(1);

        (await db.ReturnTemplates.CountAsync(t => t.ModuleId == module.Id)).Should().Be(10);
        (await db.CrossSheetRules.CountAsync()).Should().Be(3);
        (await db.IntraSheetFormulas.CountAsync(f => f.TargetFieldName == "variance_minutes")).Should().Be(1);
        (await db.TemplateFields.CountAsync(f => f.RegulatoryReference != null && f.RegulatoryReference.StartsWith("CBN-OR-", StringComparison.OrdinalIgnoreCase))).Should().BeGreaterThan(20);

        var publish = await sut.PublishModule("OPS_RESILIENCE", "ops-resilience-approver");
        publish.Success.Should().BeTrue(string.Join(" | ", publish.Errors));
        publish.TablesCreated.Should().Be(10);

        ddlExecutor.Verify(
            e => e.Execute(
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<int>(),
                It.IsAny<DdlScript>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(10));

        cache.Verify(c => c.InvalidateModule(module.Id), Times.Once);
    }

    private static MetadataDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

        return new MetadataDbContext(options);
    }

    private static async Task<Module> SeedModules(MetadataDbContext db, params string[] moduleCodes)
    {
        Module? first = null;
        foreach (var code in moduleCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var module = new Module
            {
                ModuleCode = code,
                ModuleName = $"{code} Module",
                RegulatorCode = "CBN",
                DefaultFrequency = "Quarterly",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Modules.Add(module);
            first ??= module;
        }

        await db.SaveChangesAsync();
        return first!;
    }

    private static ModuleImportService CreateSut(
        MetadataDbContext db,
        Mock<ITemplateMetadataCache> cache,
        out Mock<IDdlMigrationExecutor> ddlExecutor)
    {
        var ddlEngine = new Mock<IDdlEngine>();
        ddlEngine.Setup(d => d.GenerateCreateTable(It.IsAny<FC.Engine.Domain.Metadata.ReturnTemplate>(), It.IsAny<FC.Engine.Domain.Metadata.TemplateVersion>()))
            .Returns(new DdlScript("CREATE TABLE dbo.[tmp_ops_resilience](id INT, TenantId UNIQUEIDENTIFIER NULL);", "DROP TABLE dbo.[tmp_ops_resilience];"));
        ddlEngine.Setup(d => d.GenerateAlterTable(It.IsAny<FC.Engine.Domain.Metadata.ReturnTemplate>(), It.IsAny<FC.Engine.Domain.Metadata.TemplateVersion>(), It.IsAny<FC.Engine.Domain.Metadata.TemplateVersion>()))
            .Returns(new DdlScript("ALTER TABLE dbo.[tmp_ops_resilience] ADD test_col INT NULL;", "ALTER TABLE dbo.[tmp_ops_resilience] DROP COLUMN test_col;"));

        ddlExecutor = new Mock<IDdlMigrationExecutor>();
        ddlExecutor.Setup(e => e.Execute(
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<int>(),
                It.IsAny<DdlScript>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MigrationResult(true, null));

        ddlExecutor.Setup(e => e.Rollback(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MigrationResult(true, null));

        return new ModuleImportService(
            db,
            ddlEngine.Object,
            ddlExecutor.Object,
            cache.Object,
            new SqlTypeMapper(),
            NullLogger<ModuleImportService>.Instance,
            null);
    }

    private static async Task<string> LoadDefinition(string fileName)
    {
        var root = FindSolutionRoot();
        var path = Path.Combine(
            root,
            "src",
            "FC.Engine.Migrator",
            "SeedData",
            "ModuleDefinitions",
            fileName);

        File.Exists(path).Should().BeTrue($"Expected OPS_RESILIENCE definition file at {path}");
        return await File.ReadAllTextAsync(path);
    }

    private static string FindSolutionRoot()
    {
        var current = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(current);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "FCEngine.sln");
            if (File.Exists(candidate))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate FCEngine.sln from test base directory.");
    }
}
