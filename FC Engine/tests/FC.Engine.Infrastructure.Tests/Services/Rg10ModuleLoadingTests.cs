using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.DataRecord;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Infrastructure.DynamicSchema;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FC.Engine.Infrastructure.Validation;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FC.Engine.Infrastructure.Tests.Services;

public class Rg10ModuleLoadingTests
{
    [Fact]
    public async Task RG10_Definitions_Validate_Import_And_Publish_All_Modules_At_Expected_Scale()
    {
        await using var db = CreateDbContext(nameof(RG10_Definitions_Validate_Import_And_Publish_All_Modules_At_Expected_Scale));
        await SeedModules(
            db,
            "INSURANCE_NAICOM",
            "PFA_PENCOM",
            "CMO_SEC",
            "DFI_CBN",
            "IMTO_CBN",
            "NFIU_AML",
            "ESG_CLIMATE");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out var ddlExecutor);

        foreach (var file in new[]
                 {
                     "insurance_naicom.json",
                     "pfa_pencom.json",
                     "cmo_sec.json",
                     "dfi_cbn.json",
                     "imto_cbn.json"
                 })
        {
            var definition = await LoadDefinition(file);
            var validation = await sut.ValidateDefinition(definition);
            validation.IsValid.Should().BeTrue(string.Join(" | ", validation.Errors));

            var import = await sut.ImportModule(definition, "rg10-test");
            import.Success.Should().BeTrue(string.Join(" | ", import.Errors));
        }

        (await db.ReturnTemplates.CountAsync()).Should().Be(61);
        (await db.TemplateFields.CountAsync()).Should().BeGreaterOrEqualTo(1300);
        (await db.IntraSheetFormulas.CountAsync()).Should().BeGreaterOrEqualTo(300);

        foreach (var code in new[] { "INSURANCE_NAICOM", "PFA_PENCOM", "CMO_SEC", "DFI_CBN", "IMTO_CBN" })
        {
            var publish = await sut.PublishModule(code, "rg10-approver");
            publish.Success.Should().BeTrue(string.Join(" | ", publish.Errors));
        }

        ddlExecutor.Verify(
            e => e.Execute(
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<int>(),
                It.IsAny<DdlScript>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(61));
    }

    [Fact]
    public async Task Insurance_Solvency_And_Combined_Ratio_Calculate_Correctly()
    {
        await using var db = CreateDbContext(nameof(Insurance_Solvency_And_Combined_Ratio_Calculate_Correctly));
        await SeedModules(db, "INSURANCE_NAICOM", "NFIU_AML");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadDefinition("insurance_naicom.json"), "rg10-test")).Success.Should().BeTrue();

        var errors = await EvaluateImportedTemplateFormulas(
            db,
            "INS_SOL",
            new Dictionary<string, object?>
            {
                ["admitted_assets"] = 150m,
                ["total_liabilities"] = 90m,
                ["solvency_margin"] = 60m,
                ["minimum_capital_requirement"] = 50m,
                ["solvency_capital_requirement"] = 55m,
                ["capital_surplus"] = 10m,
                ["solvency_ratio"] = 1.09m,
                ["claims_ratio"] = 65m,
                ["expense_ratio"] = 25m,
                ["combined_ratio"] = 90m,
                ["target_combined_ratio"] = 100m
            });

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task PFA_MultiFund_Nav_Computes_Correctly()
    {
        await using var db = CreateDbContext(nameof(PFA_MultiFund_Nav_Computes_Correctly));
        await SeedModules(db, "PFA_PENCOM", "NFIU_AML");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadDefinition("pfa_pencom.json"), "rg10-test")).Success.Should().BeTrue();

        var errors = await EvaluateImportedTemplateFormulas(
            db,
            "PFA_NAV",
            new Dictionary<string, object?>
            {
                ["fund_i_assets"] = 100m,
                ["fund_i_units"] = 10m,
                ["fund_i_nav_per_unit"] = 10m,
                ["fund_ii_assets"] = 200m,
                ["fund_ii_units"] = 20m,
                ["fund_ii_nav_per_unit"] = 10m,
                ["fund_iii_assets"] = 300m,
                ["fund_iii_units"] = 30m,
                ["fund_iii_nav_per_unit"] = 10m,
                ["fund_iv_assets"] = 400m,
                ["fund_iv_units"] = 40m,
                ["fund_iv_nav_per_unit"] = 10m,
                ["fund_v_assets"] = 500m,
                ["fund_v_units"] = 50m,
                ["fund_v_nav_per_unit"] = 10m,
                ["fund_vi_assets"] = 600m,
                ["fund_vi_units"] = 60m,
                ["fund_vi_nav_per_unit"] = 10m,
                ["total_nav"] = 2100m,
                ["total_units"] = 210m,
                ["weighted_nav_per_unit"] = 10m
            });

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task PFA_Asset_Allocation_Checks_Against_PenCom_Limits()
    {
        await using var db = CreateDbContext(nameof(PFA_Asset_Allocation_Checks_Against_PenCom_Limits));
        await SeedModules(db, "PFA_PENCOM", "NFIU_AML");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadDefinition("pfa_pencom.json"), "rg10-test")).Success.Should().BeTrue();

        var errors = await EvaluateImportedTemplateFormulas(
            db,
            "PFA_AAL",
            new Dictionary<string, object?>
            {
                ["equity_percent"] = 30m,
                ["money_market_percent"] = 20m,
                ["fgn_bond_percent"] = 20m,
                ["corp_bond_percent"] = 10m,
                ["infrastructure_percent"] = 8m,
                ["private_equity_percent"] = 12m,
                ["max_equity_percent"] = 25m,
                ["max_money_market_percent"] = 35m,
                ["max_private_equity_percent"] = 10m,
                ["total_allocation_percent"] = 100m,
                ["allocation_target_percent"] = 100m
            });

        errors.Should().Contain(e => e.Field == "equity_percent");
        errors.Should().Contain(e => e.Field == "private_equity_percent");
    }

    [Fact]
    public async Task CMO_Net_Capital_Calculates_Correctly()
    {
        await using var db = CreateDbContext(nameof(CMO_Net_Capital_Calculates_Correctly));
        await SeedModules(db, "CMO_SEC", "NFIU_AML", "ESG_CLIMATE");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadDefinition("cmo_sec.json"), "rg10-test")).Success.Should().BeTrue();

        var errors = await EvaluateImportedTemplateFormulas(
            db,
            "CMO_CAP",
            new Dictionary<string, object?>
            {
                ["liquid_assets"] = 500m,
                ["total_liabilities"] = 300m,
                ["net_capital"] = 200m,
                ["minimum_net_capital"] = 150m,
                ["net_capital_ratio"] = 1.33m
            });

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task CMO_Client_Asset_Segregation_Validation_Fails_When_Proprietary_Exceeds_Client()
    {
        await using var db = CreateDbContext(nameof(CMO_Client_Asset_Segregation_Validation_Fails_When_Proprietary_Exceeds_Client));
        await SeedModules(db, "CMO_SEC", "NFIU_AML", "ESG_CLIMATE");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadDefinition("cmo_sec.json"), "rg10-test")).Success.Should().BeTrue();

        var errors = await EvaluateImportedTemplateFormulas(
            db,
            "CMO_CLI",
            new Dictionary<string, object?>
            {
                ["client_cash"] = 50m,
                ["client_securities"] = 30m,
                ["client_total_assets"] = 80m,
                ["proprietary_assets"] = 120m,
                ["segregation_buffer"] = -40m
            });

        errors.Should().Contain(e => e.Field == "client_total_assets");
    }

    [Fact]
    public async Task DFI_Sector_Allocation_Must_Total_100()
    {
        await using var db = CreateDbContext(nameof(DFI_Sector_Allocation_Must_Total_100));
        await SeedModules(db, "DFI_CBN", "NFIU_AML", "ESG_CLIMATE");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadDefinition("dfi_cbn.json"), "rg10-test")).Success.Should().BeTrue();

        var errors = await EvaluateImportedTemplateFormulas(
            db,
            "DFI_SEC",
            new Dictionary<string, object?>
            {
                ["agriculture_percent"] = 20m,
                ["sme_percent"] = 20m,
                ["health_percent"] = 15m,
                ["education_percent"] = 10m,
                ["renewable_percent"] = 10m,
                ["infrastructure_percent"] = 20m,
                ["total_sector_allocation_percent"] = 95m,
                ["sector_allocation_target_percent"] = 100m,
                ["agriculture_exposure"] = 200m,
                ["renewable_exposure"] = 100m,
                ["total_sector_exposure"] = 300m
            });

        errors.Should().Contain(e => e.Field == "total_sector_allocation_percent");
    }

    [Fact]
    public async Task IMTO_Corridor_Volumes_Reconcile_With_Financial_Statements_Rule_Is_Present()
    {
        await using var db = CreateDbContext(nameof(IMTO_Corridor_Volumes_Reconcile_With_Financial_Statements_Rule_Is_Present));
        await SeedModules(db, "IMTO_CBN", "NFIU_AML");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadDefinition("imto_cbn.json"), "rg10-test")).Success.Should().BeTrue();

        var rule = await db.CrossSheetRules.SingleOrDefaultAsync(r =>
            r.SourceTemplateCode == "IMTO_COR"
            && r.TargetTemplateCode == "IMTO_FIN"
            && r.SourceFieldCode == "total_corridor_value"
            && r.TargetFieldCode == "total_remittance_value");

        rule.Should().NotBeNull();
        rule!.Operator.Should().Be("Equals");
    }

    [Fact]
    public async Task RG10_AML_Flows_Target_NFIU_For_All_Modules()
    {
        await using var db = CreateDbContext(nameof(RG10_AML_Flows_Target_NFIU_For_All_Modules));
        await SeedModules(
            db,
            "INSURANCE_NAICOM",
            "PFA_PENCOM",
            "CMO_SEC",
            "DFI_CBN",
            "IMTO_CBN",
            "NFIU_AML",
            "ESG_CLIMATE");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);

        foreach (var file in new[]
                 {
                     "insurance_naicom.json",
                     "pfa_pencom.json",
                     "cmo_sec.json",
                     "dfi_cbn.json",
                     "imto_cbn.json"
                 })
        {
            (await sut.ImportModule(await LoadDefinition(file), "rg10-test")).Success.Should().BeTrue();
        }

        var amlFlowsToNfiu = await db.InterModuleDataFlows
            .CountAsync(f =>
                f.TargetModuleCode == "NFIU_AML"
                && f.SourceTemplateCode.EndsWith("AML", StringComparison.OrdinalIgnoreCase));

        amlFlowsToNfiu.Should().Be(15);

        var modulesWithAmlFlows = await db.InterModuleDataFlows
            .Where(f => f.TargetModuleCode == "NFIU_AML" && f.SourceTemplateCode.EndsWith("AML"))
            .Select(f => f.SourceModuleId)
            .Distinct()
            .CountAsync();

        modulesWithAmlFlows.Should().Be(5);
    }

    [Fact]
    public async Task Insurance_Module_Full_Return_Lifecycle()
    {
        await AssertModuleLifecycle(
            nameof(Insurance_Module_Full_Return_Lifecycle),
            "INSURANCE_NAICOM",
            "INS_COV",
            "insurance_naicom.json");
    }

    [Fact]
    public async Task PFA_Module_Full_Return_Lifecycle()
    {
        await AssertModuleLifecycle(
            nameof(PFA_Module_Full_Return_Lifecycle),
            "PFA_PENCOM",
            "PFA_COV",
            "pfa_pencom.json");
    }

    [Fact]
    public async Task CMO_Module_Full_Return_Lifecycle()
    {
        await AssertModuleLifecycle(
            nameof(CMO_Module_Full_Return_Lifecycle),
            "CMO_SEC",
            "CMO_COV",
            "cmo_sec.json");
    }

    [Fact]
    public async Task DFI_Module_Full_Return_Lifecycle()
    {
        await AssertModuleLifecycle(
            nameof(DFI_Module_Full_Return_Lifecycle),
            "DFI_CBN",
            "DFI_COV",
            "dfi_cbn.json");
    }

    [Fact]
    public async Task IMTO_Module_Full_Return_Lifecycle()
    {
        await AssertModuleLifecycle(
            nameof(IMTO_Module_Full_Return_Lifecycle),
            "IMTO_CBN",
            "IMTO_COV",
            "imto_cbn.json");
    }

    private static async Task AssertModuleLifecycle(
        string dbName,
        string moduleCode,
        string returnCode,
        string definitionFile)
    {
        await using var db = CreateDbContext(dbName);
        await SeedModules(
            db,
            "INSURANCE_NAICOM",
            "PFA_PENCOM",
            "CMO_SEC",
            "DFI_CBN",
            "IMTO_CBN",
            "NFIU_AML",
            "ESG_CLIMATE");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);

        (await sut.ImportModule(await LoadDefinition(definitionFile), "rg10-test")).Success.Should().BeTrue();
        var publish = await sut.PublishModule(moduleCode, "rg10-approver");
        publish.Success.Should().BeTrue(string.Join(" | ", publish.Errors));

        var template = await db.ReturnTemplates.SingleAsync(t => t.ReturnCode == returnCode);
        var version = await db.TemplateVersions
            .OrderByDescending(v => v.VersionNumber)
            .FirstAsync(v => v.TemplateId == template.Id);

        version.Status.Should().Be(TemplateStatus.Published);

        var submission = Submission.Create(77, 202603, returnCode, Guid.NewGuid());
        submission.SetTemplateVersion(version.Id);
        submission.MarkParsing();
        submission.MarkValidating();
        submission.MarkPendingApproval();
        submission.MarkAccepted();

        submission.Status.Should().Be(SubmissionStatus.Accepted);
        submission.TemplateVersionId.Should().Be(version.Id);
    }

    private static async Task<IReadOnlyList<ValidationError>> EvaluateImportedTemplateFormulas(
        MetadataDbContext db,
        string returnCode,
        IDictionary<string, object?> fieldValues)
    {
        var templateId = await db.ReturnTemplates
            .Where(t => t.ReturnCode == returnCode)
            .Select(t => t.Id)
            .SingleAsync();

        var versionId = await db.TemplateVersions
            .Where(v => v.TemplateId == templateId)
            .Select(v => v.Id)
            .SingleAsync();

        var formulas = await db.IntraSheetFormulas
            .Where(f => f.TemplateVersionId == versionId && f.IsActive)
            .OrderBy(f => f.SortOrder)
            .ToListAsync();

        var cache = new Mock<ITemplateMetadataCache>();
        cache.Setup(c => c.GetPublishedTemplate(returnCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedTemplate
            {
                TemplateId = templateId,
                ReturnCode = returnCode,
                StructuralCategory = StructuralCategory.FixedRow.ToString(),
                CurrentVersion = new CachedTemplateVersion
                {
                    Id = versionId,
                    VersionNumber = 1,
                    IntraSheetFormulas = formulas
                }
            });

        var evaluator = new FormulaEvaluator(cache.Object);
        var record = new ReturnDataRecord(returnCode, 1, StructuralCategory.FixedRow);
        var row = new ReturnDataRow();
        foreach (var kvp in fieldValues)
        {
            row.SetValue(kvp.Key, kvp.Value);
        }

        record.AddRow(row);
        return await evaluator.Evaluate(record, CancellationToken.None);
    }

    private static MetadataDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

        return new MetadataDbContext(options);
    }

    private static async Task SeedModules(MetadataDbContext db, params string[] moduleCodes)
    {
        foreach (var code in moduleCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var regulatorCode = code switch
            {
                "INSURANCE_NAICOM" => "NAICOM",
                "PFA_PENCOM" => "PenCom",
                "CMO_SEC" => "SEC",
                "NFIU_AML" => "NFIU",
                _ => "CBN"
            };

            var module = new Module
            {
                ModuleCode = code,
                ModuleName = $"{code} Module",
                RegulatorCode = regulatorCode,
                DefaultFrequency = code is "INSURANCE_NAICOM" or "DFI_CBN" ? "Quarterly" : "Monthly",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Modules.Add(module);
        }

        await db.SaveChangesAsync();
    }

    private static ModuleImportService CreateSut(
        MetadataDbContext db,
        Mock<ITemplateMetadataCache> cache,
        out Mock<IDdlMigrationExecutor> ddlExecutor)
    {
        var ddlEngine = new Mock<IDdlEngine>();
        ddlEngine.Setup(d => d.GenerateCreateTable(It.IsAny<ReturnTemplate>(), It.IsAny<TemplateVersion>()))
            .Returns(new DdlScript("CREATE TABLE dbo.[tmp_rg10](id INT, TenantId UNIQUEIDENTIFIER NULL);", "DROP TABLE dbo.[tmp_rg10];"));
        ddlEngine.Setup(d => d.GenerateAlterTable(It.IsAny<ReturnTemplate>(), It.IsAny<TemplateVersion>(), It.IsAny<TemplateVersion>()))
            .Returns(new DdlScript("ALTER TABLE dbo.[tmp_rg10] ADD test_col INT NULL;", "ALTER TABLE dbo.[tmp_rg10] DROP COLUMN test_col;"));

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
        var path = Path.Combine(root, "docs", "module-definitions", "rg10", fileName);
        File.Exists(path).Should().BeTrue($"Expected RG-10 definition file at {path}");
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
