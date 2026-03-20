using System.Text.Json;
using FluentAssertions;

namespace FC.Engine.Infrastructure.Tests.Services;

public class Rg09TemplateCoverageTests
{
    [Fact]
    public async Task Dmb_And_Ndic_Definitions_Declare_Source_Sheet_Coverage_For_Legacy_Sheets()
    {
        using var dmb = await LoadDefinition("dmb_basel3.json");
        using var ndic = await LoadDefinition("ndic_returns.json");

        var dmbCovered = GetCoveredSheets(dmb.RootElement);
        dmbCovered.Should().Contain("AQR - Asset Quality");
        dmbCovered.Should().Contain("FXP - FX Position");
        dmbCovered.Should().Contain("LEX - Large Exposures");
        dmbCovered.Should().Contain("STR - Stress Testing");

        var ndicCovered = GetCoveredSheets(ndic.RootElement);
        ndicCovered.Should().Contain("SPC - Special Returns");
        ndicCovered.Should().Contain("EXM - Examination Support");
        ndicCovered.Should().Contain("CLM - Claims & Resolution");
        ndicCovered.Should().Contain("RPT - Periodic Returns");
    }

    [Fact]
    public async Task Dmb_Definition_Covers_Aqr_Fxp_Lex_And_Str_Key_Fields_And_Formulas()
    {
        using var dmb = await LoadDefinition("dmb_basel3.json");

        var mkr = GetTemplate(dmb.RootElement, "DMB_MKR");
        HasField(mkr, "fxp_shareholders_funds").Should().BeTrue();
        HasField(mkr, "fxp_usd_net_open_position").Should().BeTrue();
        HasField(mkr, "fxp_single_currency_limit_percent_shf").Should().BeTrue();
        HasFormula(mkr, "LessThanOrEqual", "fxp_aggregate_nop_percent_shf").Should().BeTrue();

        var npl = GetTemplate(dmb.RootElement, "DMB_NPL");
        HasField(npl, "specific_provisions").Should().BeTrue();
        HasField(npl, "interest_in_suspense").Should().BeTrue();
        HasField(npl, "loans_written_off_quarter").Should().BeTrue();
        HasFormula(npl, "Difference", "net_write_off_impact").Should().BeTrue();

        var ifr = GetTemplate(dmb.RootElement, "DMB_IFR");
        HasField(ifr, "poci_gross_carrying_amount").Should().BeTrue();
        HasField(ifr, "poci_ecl_allowance").Should().BeTrue();
        HasFormula(ifr, "Difference", "poci_net_carrying_amount").Should().BeTrue();

        var lnd = GetTemplate(dmb.RootElement, "DMB_LND");
        HasField(lnd, "lex_top20_total_exposure").Should().BeTrue();
        HasField(lnd, "lex_connected_exposure_percent_shf").Should().BeTrue();
        HasFormula(lnd, "LessThanOrEqual", "lex_connected_exposure_percent_shf").Should().BeTrue();

        var cap = GetTemplate(dmb.RootElement, "DMB_CAP");
        HasField(cap, "stress_baseline_gdp_growth").Should().BeTrue();
        HasField(cap, "stress_total_car_severe").Should().BeTrue();
        HasFormula(cap, "GreaterThanOrEqual", "stress_total_car_severe").Should().BeTrue();
    }

    [Fact]
    public async Task Ndic_Definition_Covers_Spc_Exm_Clm_And_Rpt_Key_Fields_And_Formulas()
    {
        using var ndic = await LoadDefinition("ndic_returns.json");

        var ews = GetTemplate(ndic.RootElement, "NDIC_EWS");
        HasField(ews, "spc_trigger_event").Should().BeTrue();
        HasField(ews, "spc_condition_report_reference").Should().BeTrue();
        HasFormula(ews, "Sum", "spc_trigger_breach_count").Should().BeTrue();

        var gov = GetTemplate(ndic.RootElement, "NDIC_GOV");
        HasField(gov, "exm_last_exam_date").Should().BeTrue();
        HasField(gov, "exm_directives_issued").Should().BeTrue();
        HasFormula(gov, "LessThanOrEqual", "exm_directives_complied").Should().BeTrue();

        var pay = GetTemplate(ndic.RootElement, "NDIC_PAY");
        HasField(pay, "clm_total_claims_filed").Should().BeTrue();
        HasField(pay, "clm_total_insured_amount_payable").Should().BeTrue();
        HasFormula(pay, "LessThanOrEqual", "clm_amount_paid_to_depositors").Should().BeTrue();

        var dic = GetTemplate(ndic.RootElement, "NDIC_DIC");
        HasField(dic, "rpt_reporting_period").Should().BeTrue();
        HasField(dic, "rpt_periodic_returns_compliance_score").Should().BeTrue();
        HasFormula(dic, "Ratio", "rpt_quarterly_submission_rate").Should().BeTrue();
        HasFormula(dic, "Sum", "rpt_periodic_returns_compliance_score").Should().BeTrue();
    }

    private static HashSet<string> GetCoveredSheets(JsonElement root)
    {
        var sheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in root.GetProperty("templates").EnumerateArray())
        {
            if (!template.TryGetProperty("sourceSheetsCovered", out var covered))
            {
                continue;
            }

            foreach (var sheet in covered.EnumerateArray())
            {
                sheets.Add(sheet.GetString() ?? string.Empty);
            }
        }

        return sheets;
    }

    private static JsonElement GetTemplate(JsonElement root, string returnCode)
    {
        foreach (var template in root.GetProperty("templates").EnumerateArray())
        {
            if (string.Equals(template.GetProperty("returnCode").GetString(), returnCode, StringComparison.OrdinalIgnoreCase))
            {
                return template;
            }
        }

        throw new InvalidOperationException($"Template {returnCode} not found.");
    }

    private static bool HasField(JsonElement template, string fieldCode)
    {
        foreach (var field in template.GetProperty("fields").EnumerateArray())
        {
            if (string.Equals(field.GetProperty("fieldCode").GetString(), fieldCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasFormula(JsonElement template, string formulaType, string targetField)
    {
        foreach (var formula in template.GetProperty("formulas").EnumerateArray())
        {
            var currentType = formula.GetProperty("formulaType").GetString();
            var currentTarget = formula.GetProperty("targetField").GetString();
            if (string.Equals(currentType, formulaType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(currentTarget, targetField, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<JsonDocument> LoadDefinition(string fileName)
    {
        var root = FindSolutionRoot();
        var path = Path.Combine(root, "docs", "module-definitions", "rg09", fileName);
        File.Exists(path).Should().BeTrue($"Expected RG-09 definition file at {path}");
        var json = await File.ReadAllTextAsync(path);
        return JsonDocument.Parse(json);
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
