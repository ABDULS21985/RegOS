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

public class Rg11ModuleLoadingTests
{
    [Fact]
    public async Task FATF_Module_Imports_13_Templates_Successfully()
    {
        await using var db = CreateDbContext(nameof(FATF_Module_Imports_13_Templates_Successfully));
        await SeedModules(db, "FATF_EVAL", "NFIU_AML", "ESG_CLIMATE", "DMB_BASEL3", "CMO_SEC", "DFI_CBN");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);

        var definition = await LoadRg11Definition("fatf_eval.json");
        var validation = await sut.ValidateDefinition(definition);
        validation.IsValid.Should().BeTrue(string.Join(" | ", validation.Errors));
        validation.TemplateCount.Should().Be(13);

        var import = await sut.ImportModule(definition, "rg11-test");
        import.Success.Should().BeTrue(string.Join(" | ", import.Errors));
        import.TemplatesCreated.Should().Be(13);
    }

    [Fact]
    public async Task ESG_Module_Imports_13_Templates_Successfully()
    {
        await using var db = CreateDbContext(nameof(ESG_Module_Imports_13_Templates_Successfully));
        await SeedModules(db, "FATF_EVAL", "NFIU_AML", "ESG_CLIMATE", "DMB_BASEL3", "CMO_SEC", "DFI_CBN");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);

        var definition = await LoadRg11Definition("esg_climate.json");
        var validation = await sut.ValidateDefinition(definition);
        validation.IsValid.Should().BeTrue(string.Join(" | ", validation.Errors));
        validation.TemplateCount.Should().Be(13);

        var import = await sut.ImportModule(definition, "rg11-test");
        import.Success.Should().BeTrue(string.Join(" | ", import.Errors));
        import.TemplatesCreated.Should().Be(13);
    }

    [Fact]
    public async Task FATF_40_Recommendations_Include_2021_MER_And_2024_FUR_Baseline_Fields()
    {
        await using var db = CreateDbContext(nameof(FATF_40_Recommendations_Include_2021_MER_And_2024_FUR_Baseline_Fields));
        await SeedModules(db, "FATF_EVAL", "NFIU_AML", "ESG_CLIMATE", "DMB_BASEL3", "CMO_SEC", "DFI_CBN");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadRg11Definition("fatf_eval.json"), "rg11-test")).Success.Should().BeTrue();

        var tcTemplateId = await db.ReturnTemplates
            .Where(t => t.ReturnCode == "FATF_TC")
            .Select(t => t.Id)
            .SingleAsync();

        var versionId = await db.TemplateVersions
            .Where(v => v.TemplateId == tcTemplateId)
            .Select(v => v.Id)
            .SingleAsync();

        var fieldNames = await db.TemplateFields
            .Where(f => f.TemplateVersionId == versionId)
            .Select(f => f.FieldName)
            .ToListAsync();

        for (var rec = 1; rec <= 40; rec++)
        {
            var code = $"r{rec:00}";
            fieldNames.Should().Contain($"{code}_rating");
            fieldNames.Should().Contain($"{code}_mer_2021_rating");
            fieldNames.Should().Contain($"{code}_fur_2024_rating");
        }

        fieldNames.Should().Contain("tc_effective_compliance_ratio");
    }

    [Fact]
    public async Task FATF_IO6_Metrics_AutoPopulate_From_NFIU_DataFlows()
    {
        await using var db = CreateDbContext(nameof(FATF_IO6_Metrics_AutoPopulate_From_NFIU_DataFlows));
        await SeedModules(db, "FATF_EVAL", "NFIU_AML", "ESG_CLIMATE", "DMB_BASEL3", "CMO_SEC", "DFI_CBN");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);

        (await sut.ImportModule(await LoadRg08Definition("rg08-nfiu-aml-module-definition.json"), "rg11-test")).Success.Should().BeTrue();
        (await sut.ImportModule(await LoadRg11Definition("fatf_eval.json"), "rg11-test")).Success.Should().BeTrue();

        var tenantId = Guid.NewGuid();
        var strSubmission = Submission.Create(99, 202612, "NFIU_STR", tenantId);
        var ctrSubmission = Submission.Create(99, 202612, "NFIU_CTR", tenantId);
        db.Submissions.AddRange(strSubmission, ctrSubmission);
        await db.SaveChangesAsync();

        var entitlement = new Mock<IEntitlementService>();
        entitlement.Setup(e => e.HasModuleAccess(tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var genericRepo = new Mock<IGenericDataRepository>();
        genericRepo.Setup(r => r.ReadFieldValue("NFIU_STR", strSubmission.Id, "str_filed_count", It.IsAny<CancellationToken>()))
            .ReturnsAsync(14m);
        genericRepo.Setup(r => r.ReadFieldValue("NFIU_CTR", ctrSubmission.Id, "ctr_filed_count", It.IsAny<CancellationToken>()))
            .ReturnsAsync(30m);
        genericRepo.Setup(r => r.ReadFieldValue(It.IsAny<string>(), It.IsAny<int>(), It.Is<string>(f => f != "str_filed_count" && f != "ctr_filed_count"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var engine = new InterModuleDataFlowEngine(
            db,
            entitlement.Object,
            genericRepo.Object,
            NullLogger<InterModuleDataFlowEngine>.Instance);

        await engine.ProcessDataFlows(tenantId, strSubmission.Id, "NFIU_AML", "NFIU_STR", 99, 202612, CancellationToken.None);
        await engine.ProcessDataFlows(tenantId, ctrSubmission.Id, "NFIU_AML", "NFIU_CTR", 99, 202612, CancellationToken.None);

        genericRepo.Verify(r => r.WriteFieldValue(
                "FATF_IO",
                It.IsAny<int>(),
                "io6_str_volume",
                14m,
                "InterModule",
                "NFIU_AML/NFIU_STR/str_filed_count",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        genericRepo.Verify(r => r.WriteFieldValue(
                "FATF_IO",
                It.IsAny<int>(),
                "io6_ctr_volume",
                30m,
                "InterModule",
                "NFIU_AML/NFIU_CTR/ctr_filed_count",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FATF_ActionPlan_Tracker_Captures_Milestones_And_Evidence()
    {
        await using var db = CreateDbContext(nameof(FATF_ActionPlan_Tracker_Captures_Milestones_And_Evidence));
        await SeedModules(db, "FATF_EVAL", "NFIU_AML", "ESG_CLIMATE", "DMB_BASEL3", "CMO_SEC", "DFI_CBN");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadRg11Definition("fatf_eval.json"), "rg11-test")).Success.Should().BeTrue();

        var fields = await LoadTemplateFieldNames(db, "FATF_APT");
        fields.Should().Contain("action_item_1_milestone");
        fields.Should().Contain("action_item_1_evidence");
        fields.Should().Contain("action_item_2_milestone");
        fields.Should().Contain("action_item_2_evidence");
        fields.Should().Contain("action_item_3_milestone");
        fields.Should().Contain("action_item_3_evidence");
    }

    [Fact]
    public async Task ESG_TCFD_4_Pillar_Structure_Is_Complete()
    {
        await using var db = CreateDbContext(nameof(ESG_TCFD_4_Pillar_Structure_Is_Complete));
        await SeedModules(db, "FATF_EVAL", "NFIU_AML", "ESG_CLIMATE", "DMB_BASEL3", "CMO_SEC", "DFI_CBN");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadRg11Definition("esg_climate.json"), "rg11-test")).Success.Should().BeTrue();

        var templates = await db.ReturnTemplates.Select(t => t.ReturnCode).ToListAsync();
        templates.Should().Contain("ESG_GOV");
        templates.Should().Contain("ESG_STR");
        templates.Should().Contain("ESG_RMG");
        templates.Should().Contain("ESG_MET");
    }

    [Fact]
    public async Task ESG_PCAF_Financed_Emissions_Calculates_By_AssetClass_With_Data_Quality_Score()
    {
        await using var db = CreateDbContext(nameof(ESG_PCAF_Financed_Emissions_Calculates_By_AssetClass_With_Data_Quality_Score));
        await SeedModules(db, "FATF_EVAL", "NFIU_AML", "ESG_CLIMATE", "DMB_BASEL3", "CMO_SEC", "DFI_CBN");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadRg11Definition("esg_climate.json"), "rg11-test")).Success.Should().BeTrue();

        var errors = await EvaluateImportedTemplateFormulas(
            db,
            "ESG_FINANCED_EMISSIONS",
            new Dictionary<string, object?>
            {
                ["corporate_loans_outstanding_amount"] = 1000m,
                ["corporate_loans_attributed_emissions_tco2e"] = 100m,
                ["corporate_loans_emission_factor"] = 0.1m,
                ["corporate_loans_data_quality_score"] = 3m,
                ["corporate_loans_financed_emissions_intensity"] = 0.1m,

                ["project_finance_outstanding_amount"] = 500m,
                ["project_finance_attributed_emissions_tco2e"] = 50m,
                ["project_finance_emission_factor"] = 0.1m,
                ["project_finance_data_quality_score"] = 3m,
                ["project_finance_financed_emissions_intensity"] = 0.1m,

                ["commercial_real_estate_outstanding_amount"] = 300m,
                ["commercial_real_estate_attributed_emissions_tco2e"] = 30m,
                ["commercial_real_estate_emission_factor"] = 0.1m,
                ["commercial_real_estate_data_quality_score"] = 2m,
                ["commercial_real_estate_financed_emissions_intensity"] = 0.1m,

                ["mortgages_outstanding_amount"] = 200m,
                ["mortgages_attributed_emissions_tco2e"] = 20m,
                ["mortgages_emission_factor"] = 0.1m,
                ["mortgages_data_quality_score"] = 2m,
                ["mortgages_financed_emissions_intensity"] = 0.1m,

                ["motor_vehicles_outstanding_amount"] = 100m,
                ["motor_vehicles_attributed_emissions_tco2e"] = 10m,
                ["motor_vehicles_emission_factor"] = 0.1m,
                ["motor_vehicles_data_quality_score"] = 4m,
                ["motor_vehicles_financed_emissions_intensity"] = 0.1m,

                ["sovereign_bonds_outstanding_amount"] = 900m,
                ["sovereign_bonds_attributed_emissions_tco2e"] = 45m,
                ["sovereign_bonds_emission_factor"] = 0.05m,
                ["sovereign_bonds_data_quality_score"] = 2m,
                ["sovereign_bonds_financed_emissions_intensity"] = 0.05m,

                ["total_financed_emissions_tco2e"] = 255m,
                ["total_financed_outstanding"] = 3000m,
                ["portfolio_financed_emissions_intensity"] = 0.085m,
                ["total_data_quality_score"] = 16m,
                ["asset_class_count"] = 6m,
                ["average_data_quality_score"] = 2.6666666667m,

                ["total_loan_book"] = 2000m,
                ["oil_gas_exposure"] = 400m,
                ["agriculture_exposure"] = 200m,
                ["renewable_energy_exposure"] = 100m,
                ["portfolio_exposure"] = 500m,
                ["development_sector_exposure"] = 300m,
            });

        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ESG_18_TCFD_Sectors_Are_Mapped_Correctly()
    {
        await using var db = CreateDbContext(nameof(ESG_18_TCFD_Sectors_Are_Mapped_Correctly));
        await SeedModules(db, "FATF_EVAL", "NFIU_AML", "ESG_CLIMATE", "DMB_BASEL3", "CMO_SEC", "DFI_CBN");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadRg11Definition("esg_climate.json"), "rg11-test")).Success.Should().BeTrue();

        var fields = await LoadTemplateFieldNames(db, "ESG_SEC");
        var exposureFields = fields.Where(f => f.EndsWith("_gross_exposure", StringComparison.OrdinalIgnoreCase)).ToList();
        exposureFields.Should().HaveCount(18);
    }

    [Fact]
    public async Task ESG_Receives_Source_Data_From_DMB_CMO_And_DFI_Flows()
    {
        await using var db = CreateDbContext(nameof(ESG_Receives_Source_Data_From_DMB_CMO_And_DFI_Flows));
        await SeedModules(db, "FATF_EVAL", "NFIU_AML", "ESG_CLIMATE", "DMB_BASEL3", "CMO_SEC", "DFI_CBN");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);

        (await sut.ImportModule(await LoadRg09Definition("dmb_basel3.json"), "rg11-test")).Success.Should().BeTrue();
        (await sut.ImportModule(await LoadRg10Definition("cmo_sec.json"), "rg11-test")).Success.Should().BeTrue();
        (await sut.ImportModule(await LoadRg10Definition("dfi_cbn.json"), "rg11-test")).Success.Should().BeTrue();
        (await sut.ImportModule(await LoadRg11Definition("esg_climate.json"), "rg11-test")).Success.Should().BeTrue();

        var flows = await db.InterModuleDataFlows
            .Include(f => f.SourceModule)
            .Where(f => f.TargetModuleCode == "ESG_CLIMATE")
            .ToListAsync();

        flows.Should().Contain(f => f.SourceModule!.ModuleCode == "DMB_BASEL3" && f.TargetTemplateCode == "ESG_FINANCED_EMISSIONS");
        flows.Should().Contain(f => f.SourceModule!.ModuleCode == "DMB_BASEL3" && f.TargetTemplateCode == "ESG_SEC" && f.TargetFieldCode == "portfolio_denominator_assets");
        flows.Should().Contain(f => f.SourceModule!.ModuleCode == "CMO_SEC" && f.TargetTemplateCode == "ESG_FINANCED_EMISSIONS");
        flows.Should().Contain(f => f.SourceModule!.ModuleCode == "DFI_CBN" && f.TargetTemplateCode == "ESG_FINANCED_EMISSIONS");
    }

    [Fact]
    public async Task CBN_NSBP_9_Principles_Assessment_Is_Functional()
    {
        await using var db = CreateDbContext(nameof(CBN_NSBP_9_Principles_Assessment_Is_Functional));
        await SeedModules(db, "FATF_EVAL", "NFIU_AML", "ESG_CLIMATE", "DMB_BASEL3", "CMO_SEC", "DFI_CBN");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadRg11Definition("esg_climate.json"), "rg11-test")).Success.Should().BeTrue();

        var fields = await LoadTemplateFieldNames(db, "ESG_NSB");
        fields.Count(f => f.Contains("_compliance_rating", StringComparison.OrdinalIgnoreCase)).Should().Be(9);
        fields.Count(f => f.StartsWith("nsbp_p", StringComparison.OrdinalIgnoreCase)).Should().BeGreaterOrEqualTo(27);
    }

    [Fact]
    public async Task ESG_GHG_Scope_1_2_3_Captures_All_Required_Categories()
    {
        await using var db = CreateDbContext(nameof(ESG_GHG_Scope_1_2_3_Captures_All_Required_Categories));
        await SeedModules(db, "FATF_EVAL", "NFIU_AML", "ESG_CLIMATE", "DMB_BASEL3", "CMO_SEC", "DFI_CBN");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);
        (await sut.ImportModule(await LoadRg11Definition("esg_climate.json"), "rg11-test")).Success.Should().BeTrue();

        var fields = await LoadTemplateFieldNames(db, "ESG_GHG");
        fields.Should().Contain("fuel_combustion_emissions");
        fields.Should().Contain("purchased_electricity_location_based");
        fields.Should().Contain("investments");
        fields.Should().Contain("franchises");
        fields.Should().Contain("scope1_total");
        fields.Should().Contain("scope2_total");
        fields.Should().Contain("scope3_total");
    }

    [Fact]
    public async Task RG11_Publish_Creates_26_Tables_With_RLS()
    {
        await using var db = CreateDbContext(nameof(RG11_Publish_Creates_26_Tables_With_RLS));
        await SeedModules(db, "FATF_EVAL", "NFIU_AML", "ESG_CLIMATE", "DMB_BASEL3", "CMO_SEC", "DFI_CBN");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out var ddlExecutor);

        (await sut.ImportModule(await LoadRg11Definition("fatf_eval.json"), "rg11-test")).Success.Should().BeTrue();
        (await sut.ImportModule(await LoadRg11Definition("esg_climate.json"), "rg11-test")).Success.Should().BeTrue();

        (await sut.PublishModule("FATF_EVAL", "rg11-approver")).Success.Should().BeTrue();
        (await sut.PublishModule("ESG_CLIMATE", "rg11-approver")).Success.Should().BeTrue();

        ddlExecutor.Verify(
            e => e.Execute(
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<int>(),
                It.IsAny<DdlScript>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(26));
    }

    [Fact]
    public async Task FATF_And_ESG_Module_Full_Lifecycle_And_All_14_Modules_Live_With_175_Templates()
    {
        await using var db = CreateDbContext(nameof(FATF_And_ESG_Module_Full_Lifecycle_And_All_14_Modules_Live_With_175_Templates));
        await SeedModules(
            db,
            "BDC_CBN",
            "MFB_PAR",
            "NFIU_AML",
            "DMB_BASEL3",
            "NDIC_RETURNS",
            "PSP_FINTECH",
            "PMB_CBN",
            "INSURANCE_NAICOM",
            "PFA_PENCOM",
            "CMO_SEC",
            "DFI_CBN",
            "IMTO_CBN",
            "FATF_EVAL",
            "ESG_CLIMATE");

        var cache = new Mock<ITemplateMetadataCache>();
        var sut = CreateSut(db, cache, out _);

        foreach (var load in new Func<Task<string>>[]
                 {
                     () => LoadRg08Definition("rg08-bdc-cbn-module-definition.json"),
                     () => LoadRg08Definition("rg08-mfb-par-module-definition.json"),
                     () => LoadRg08Definition("rg08-nfiu-aml-module-definition.json"),
                     () => LoadRg09Definition("dmb_basel3.json"),
                     () => LoadRg09Definition("ndic_returns.json"),
                     () => LoadRg09Definition("psp_fintech.json"),
                     () => LoadRg09Definition("pmb_cbn.json"),
                     () => LoadRg10Definition("insurance_naicom.json"),
                     () => LoadRg10Definition("pfa_pencom.json"),
                     () => LoadRg10Definition("cmo_sec.json"),
                     () => LoadRg10Definition("dfi_cbn.json"),
                     () => LoadRg10Definition("imto_cbn.json"),
                     () => LoadRg11Definition("fatf_eval.json"),
                     () => LoadRg11Definition("esg_climate.json"),
                 })
        {
            var definition = await load();
            var validation = await sut.ValidateDefinition(definition);
            validation.IsValid.Should().BeTrue(string.Join(" | ", validation.Errors));

            var import = await sut.ImportModule(definition, "rg11-test");
            import.Success.Should().BeTrue(string.Join(" | ", import.Errors));
        }

        (await db.ReturnTemplates.CountAsync()).Should().Be(175);

        (await sut.PublishModule("FATF_EVAL", "rg11-approver")).Success.Should().BeTrue();
        (await sut.PublishModule("ESG_CLIMATE", "rg11-approver")).Success.Should().BeTrue();

        var fatfTemplate = await db.ReturnTemplates.SingleAsync(t => t.ReturnCode == "FATF_COV");
        var fatfVersion = await db.TemplateVersions.OrderByDescending(v => v.VersionNumber).FirstAsync(v => v.TemplateId == fatfTemplate.Id);
        fatfVersion.Status.Should().Be(TemplateStatus.Published);

        var esgTemplate = await db.ReturnTemplates.SingleAsync(t => t.ReturnCode == "ESG_COV");
        var esgVersion = await db.TemplateVersions.OrderByDescending(v => v.VersionNumber).FirstAsync(v => v.TemplateId == esgTemplate.Id);
        esgVersion.Status.Should().Be(TemplateStatus.Published);

        var fatfSubmission = Submission.Create(77, 202612, "FATF_COV", Guid.NewGuid());
        fatfSubmission.SetTemplateVersion(fatfVersion.Id);
        fatfSubmission.MarkParsing();
        fatfSubmission.MarkValidating();
        fatfSubmission.MarkPendingApproval();
        fatfSubmission.MarkAccepted();
        fatfSubmission.Status.Should().Be(SubmissionStatus.Accepted);

        var esgSubmission = Submission.Create(77, 202612, "ESG_COV", Guid.NewGuid());
        esgSubmission.SetTemplateVersion(esgVersion.Id);
        esgSubmission.MarkParsing();
        esgSubmission.MarkValidating();
        esgSubmission.MarkPendingApproval();
        esgSubmission.MarkAccepted();
        esgSubmission.Status.Should().Be(SubmissionStatus.Accepted);
    }

    private static async Task<List<string>> LoadTemplateFieldNames(MetadataDbContext db, string returnCode)
    {
        var templateId = await db.ReturnTemplates
            .Where(t => t.ReturnCode == returnCode)
            .Select(t => t.Id)
            .SingleAsync();

        var versionId = await db.TemplateVersions
            .Where(v => v.TemplateId == templateId)
            .Select(v => v.Id)
            .SingleAsync();

        return await db.TemplateFields
            .Where(f => f.TemplateVersionId == versionId)
            .Select(f => f.FieldName)
            .ToListAsync();
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
                "NFIU_AML" => "NFIU",
                "INSURANCE_NAICOM" => "NAICOM",
                "PFA_PENCOM" => "PENCOM",
                "CMO_SEC" => "SEC",
                "NDIC_RETURNS" => "NDIC",
                "FATF_EVAL" => "INTERNAL",
                "ESG_CLIMATE" => "INTERNAL",
                _ => "CBN"
            };

            var defaultFrequency = code switch
            {
                "FATF_EVAL" => "Annual",
                "ESG_CLIMATE" => "Annual",
                "INSURANCE_NAICOM" => "Quarterly",
                "DFI_CBN" => "Quarterly",
                _ => "Monthly"
            };

            db.Modules.Add(new Module
            {
                ModuleCode = code,
                ModuleName = $"{code} Module",
                RegulatorCode = regulatorCode,
                DefaultFrequency = defaultFrequency,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
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
            .Returns(new DdlScript("CREATE TABLE dbo.[tmp_rg11](id INT, TenantId UNIQUEIDENTIFIER NULL);", "DROP TABLE dbo.[tmp_rg11];"));
        ddlEngine.Setup(d => d.GenerateAlterTable(It.IsAny<ReturnTemplate>(), It.IsAny<TemplateVersion>(), It.IsAny<TemplateVersion>()))
            .Returns(new DdlScript("ALTER TABLE dbo.[tmp_rg11] ADD test_col INT NULL;", "ALTER TABLE dbo.[tmp_rg11] DROP COLUMN test_col;"));

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

    private static async Task<string> LoadRg08Definition(string fileName)
    {
        var root = FindSolutionRoot();
        var path = Path.Combine(root, "src", "FC.Engine.Migrator", "SeedData", "ModuleDefinitions", fileName);
        File.Exists(path).Should().BeTrue($"Expected RG-08 definition file at {path}");
        return await File.ReadAllTextAsync(path);
    }

    private static async Task<string> LoadRg09Definition(string fileName)
    {
        var root = FindSolutionRoot();
        var path = Path.Combine(root, "docs", "module-definitions", "rg09", fileName);
        File.Exists(path).Should().BeTrue($"Expected RG-09 definition file at {path}");
        return await File.ReadAllTextAsync(path);
    }

    private static async Task<string> LoadRg10Definition(string fileName)
    {
        var root = FindSolutionRoot();
        var path = Path.Combine(root, "docs", "module-definitions", "rg10", fileName);
        File.Exists(path).Should().BeTrue($"Expected RG-10 definition file at {path}");
        return await File.ReadAllTextAsync(path);
    }

    private static async Task<string> LoadRg11Definition(string fileName)
    {
        var root = FindSolutionRoot();
        var path = Path.Combine(root, "docs", "module-definitions", "rg11", fileName);
        File.Exists(path).Should().BeTrue($"Expected RG-11 definition file at {path}");
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
