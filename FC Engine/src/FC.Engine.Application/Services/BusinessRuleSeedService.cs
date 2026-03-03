using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Validation;

namespace FC.Engine.Application.Services;

public class BusinessRuleSeedService
{
    private readonly IFormulaRepository _formulaRepo;

    public BusinessRuleSeedService(IFormulaRepository formulaRepo)
    {
        _formulaRepo = formulaRepo;
    }

    /// <summary>
    /// Seeds default CBN business rules. Skips rules whose RuleCode already exists.
    /// Returns the number of rules created.
    /// </summary>
    public async Task<int> SeedDefaultRules(CancellationToken ct = default)
    {
        var existing = await _formulaRepo.GetActiveBusinessRules(ct);
        var existingCodes = existing.Select(r => r.RuleCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var defaults = GetDefaultRules();
        var created = 0;

        foreach (var rule in defaults)
        {
            if (existingCodes.Contains(rule.RuleCode))
                continue;

            await _formulaRepo.AddBusinessRule(rule, ct);
            created++;
        }

        return created;
    }

    private static List<BusinessRule> GetDefaultRules()
    {
        var now = DateTime.UtcNow;
        const string seeder = "system-seed";

        return
        [
            // ── Balance Sheet Integrity (MFCR 300) ────────────────────────

            new()
            {
                RuleCode = "BR-001",
                RuleName = "Total Assets Must Be Positive",
                Description = "The total assets reported on the Statement of Financial Position must be greater than zero.",
                RuleType = "ThresholdCheck",
                Expression = "total_assets > 0",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-002",
                RuleName = "Assets Equal Liabilities Plus Equity",
                Description = "The accounting equation must hold: Total Assets = Total Liabilities + Total Equity. A tolerance of zero is applied.",
                RuleType = "Custom",
                Expression = "total_assets = total_liabilities_and_equity",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-003",
                RuleName = "Total Cash Must Not Be Negative",
                Description = "Cash and cash equivalents cannot be negative on the balance sheet.",
                RuleType = "ThresholdCheck",
                Expression = "total_cash >= 0",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-004",
                RuleName = "Total Equity Must Not Be Negative",
                Description = "Negative equity indicates insolvency and requires immediate review by the CBN examiner.",
                RuleType = "ThresholdCheck",
                Expression = "total_equity >= 0",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Warning,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-005",
                RuleName = "Gross Loans Must Not Be Negative",
                Description = "Total gross loans and advances cannot be a negative figure.",
                RuleType = "ThresholdCheck",
                Expression = "total_gross_loans >= 0",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-006",
                RuleName = "Net Loans Must Not Exceed Gross Loans",
                Description = "Net loans (after impairment) must be less than or equal to gross loans.",
                RuleType = "Custom",
                Expression = "total_net_loans <= total_gross_loans",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Income Statement (MFCR 1000) ─────────────────────────────

            new()
            {
                RuleCode = "BR-007",
                RuleName = "Total Interest Income Must Be Positive",
                Description = "Financial institutions must report positive interest income unless they are in an exceptional loss-making position.",
                RuleType = "ThresholdCheck",
                Expression = "total_interest_income > 0",
                AppliesToTemplates = "MFCR 1000",
                Severity = ValidationSeverity.Warning,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-008",
                RuleName = "Interest Expense Must Not Be Negative",
                Description = "Total interest expense on deposits and borrowings cannot be negative.",
                RuleType = "ThresholdCheck",
                Expression = "total_interest_expense >= 0",
                AppliesToTemplates = "MFCR 1000",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Impairment / Loan Quality (MFCR 350) ─────────────────────

            new()
            {
                RuleCode = "BR-009",
                RuleName = "Impairment Allowance Must Not Be Negative",
                Description = "Loan impairment provisions must be a non-negative value.",
                RuleType = "ThresholdCheck",
                Expression = "total_impairment >= 0",
                AppliesToTemplates = "MFCR 350",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Completeness Rules (All Templates) ────────────────────────

            new()
            {
                RuleCode = "BR-010",
                RuleName = "Mandatory Total Fields Must Be Populated",
                Description = "All summary total fields across returns must be explicitly provided and not left blank.",
                RuleType = "Completeness",
                AppliesToTemplates = "MFCR 300",
                AppliesToFields = "[\"total_assets\",\"total_liabilities\",\"total_equity\",\"total_liabilities_and_equity\"]",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-011",
                RuleName = "Income Statement Totals Must Be Populated",
                Description = "Key income statement aggregates must be provided.",
                RuleType = "Completeness",
                AppliesToTemplates = "MFCR 1000",
                AppliesToFields = "[\"total_interest_income\",\"total_interest_expense\"]",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Securities & Placements ───────────────────────────────────

            new()
            {
                RuleCode = "BR-012",
                RuleName = "Treasury Bills Must Not Be Negative",
                Description = "Holdings of treasury bills must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "treasury_bills >= 0",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-013",
                RuleName = "FGN Bonds Must Not Be Negative",
                Description = "Holdings of Federal Government Bonds must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "fgn_bonds >= 0",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Borrowings & Liabilities ──────────────────────────────────

            new()
            {
                RuleCode = "BR-014",
                RuleName = "Total Borrowings Must Not Be Negative",
                Description = "Aggregate borrowings from all sources must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total_borrowings >= 0",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-015",
                RuleName = "Total Liabilities Must Not Be Negative",
                Description = "Total liabilities must be zero or positive on the balance sheet.",
                RuleType = "ThresholdCheck",
                Expression = "total_liabilities >= 0",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Cross-Template Consistency ─────────────────────────────────

            new()
            {
                RuleCode = "BR-016",
                RuleName = "Lending Above Statutory Limit Reporting",
                Description = "Institutions reporting credit above the statutory single-obligor limit must provide supporting details.",
                RuleType = "Completeness",
                AppliesToTemplates = "MFCR 1520",
                AppliesToFields = "[\"borrower_name\",\"amount_of_credit\",\"statutory_limit\"]",
                Severity = ValidationSeverity.Warning,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Date Validation ───────────────────────────────────────────

            new()
            {
                RuleCode = "BR-017",
                RuleName = "Reporting Date Must Not Be In The Future",
                Description = "All date fields in returns must not reference future dates beyond the reporting period.",
                RuleType = "DateCheck",
                AppliesToTemplates = "*",
                AppliesToFields = "[\"reporting_date\",\"transaction_date\",\"date_of_fraud\"]",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Consumer Complaints (MFCR 1600) ──────────────────────────

            new()
            {
                RuleCode = "BR-018",
                RuleName = "Complaints Resolved Must Not Exceed Total Complaints",
                Description = "The number of complaints resolved in the period cannot exceed the total complaints received.",
                RuleType = "Custom",
                Expression = "complaints_resolved <= total_complaints",
                AppliesToTemplates = "MFCR 1600",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Fraud Reporting (MFCR 1570) ──────────────────────────────

            new()
            {
                RuleCode = "BR-019",
                RuleName = "Fraud Amount Must Not Be Negative",
                Description = "The monetary amount involved in fraud/forgery cases must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "amount_involved >= 0",
                AppliesToTemplates = "MFCR 1570",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Capital Adequacy ──────────────────────────────────────────

            new()
            {
                RuleCode = "BR-020",
                RuleName = "Total Securities Must Not Be Negative",
                Description = "Aggregate securities holdings (treasury bills, bonds, etc.) must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total_securities >= 0",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ══════════════════════════════════════════════════════════════
            //  BR-021 → BR-070: Additional CBN Validation Rules
            // ══════════════════════════════════════════════════════════════

            // ── Memorandum Items (MFCR 100) ─────────────────────────────

            new()
            {
                RuleCode = "BR-021",
                RuleName = "Memorandum Items Completeness",
                Description = "All memorandum item fields must be populated when the return is submitted.",
                RuleType = "Completeness",
                AppliesToTemplates = "MFCR 100",
                Severity = ValidationSeverity.Warning,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Income Statement Schedules (MFCR 1002–1020) ────────────

            new()
            {
                RuleCode = "BR-022",
                RuleName = "Government Securities Income Must Not Be Negative",
                Description = "Income from government securities must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1002",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-023",
                RuleName = "Other Interest Income Must Not Be Negative",
                Description = "Other interest income reported in the schedule must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1004",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-024",
                RuleName = "Interest Income Breakdown Must Match Total",
                Description = "The sum of interest income breakdown items must equal the total interest income reported on MFCR 1000.",
                RuleType = "Custom",
                Expression = "breakdown_total = parent_total_interest_income",
                AppliesToTemplates = "MFCR 1006",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-025",
                RuleName = "Other Interest Expense Must Not Be Negative",
                Description = "Other interest expense items must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1008",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-026",
                RuleName = "Interest Expense Breakdown Must Match Total",
                Description = "The sum of interest expense breakdown items must equal the total interest expense reported on MFCR 1000.",
                RuleType = "Custom",
                Expression = "breakdown_total = parent_total_interest_expense",
                AppliesToTemplates = "MFCR 1010",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-027",
                RuleName = "Other Fees Must Not Be Negative",
                Description = "Fee-based income must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1012",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-028",
                RuleName = "Equity Investment Income Must Not Be Negative",
                Description = "Income from equity investments must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1014",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-029",
                RuleName = "Trading Income Must Not Be Negative",
                Description = "Other trading income must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1016",
                Severity = ValidationSeverity.Warning,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-030",
                RuleName = "Operating Expenses Must Not Be Negative",
                Description = "Other operating expenses must be reported as zero or positive values.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1020",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Balance Sheet Schedules (MFCR 302–362) ─────────────────

            new()
            {
                RuleCode = "BR-031",
                RuleName = "Balances with Banks Must Not Be Negative",
                Description = "Balances held with other banks must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 302",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-032",
                RuleName = "Secured Money at Call Must Not Be Negative",
                Description = "Secured money at call with banks must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 304",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-033",
                RuleName = "Unsecured Money at Call Must Not Be Negative",
                Description = "Unsecured money at call with banks must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 306",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-034",
                RuleName = "Secured Placements Must Not Be Negative",
                Description = "Secured placements with banks must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 308",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-035",
                RuleName = "Unsecured Placements Must Not Be Negative",
                Description = "Unsecured placements with banks must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 310",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-036",
                RuleName = "Derivative Financial Assets Must Not Be Negative",
                Description = "Derivative financial assets must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 316",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-037",
                RuleName = "State Government Bonds Must Not Be Negative",
                Description = "Holdings of state government bonds must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 322",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-038",
                RuleName = "Local Government Bonds Must Not Be Negative",
                Description = "Holdings of local government bonds must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 324",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-039",
                RuleName = "Corporate Bonds Must Not Be Negative",
                Description = "Holdings of corporate bonds must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 326",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-040",
                RuleName = "Treasury Certificates & OMO Bills Must Not Be Negative",
                Description = "Treasury certificates and OMO bills holdings must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 330",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Loans & Receivables (MFCR 338–351) ─────────────────────

            new()
            {
                RuleCode = "BR-041",
                RuleName = "Loans to Other FIs Must Not Be Negative",
                Description = "Loans and receivables to other financial institutions must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 338",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-042",
                RuleName = "Loans to Subsidiary Companies in Nigeria Must Not Be Negative",
                Description = "Loans to subsidiary companies in Nigeria must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 340",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-043",
                RuleName = "Loans to Subsidiary Companies Outside Nigeria Must Not Be Negative",
                Description = "Loans to subsidiary companies outside Nigeria must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 342",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-044",
                RuleName = "Credits to Associates in Nigeria Must Not Be Negative",
                Description = "Credits to associate/affiliate companies in Nigeria must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 344",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-045",
                RuleName = "Loans to Government Must Not Be Negative",
                Description = "Loans and receivables to government entities must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 351",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-046",
                RuleName = "Loans to Other Customers Must Not Be Negative",
                Description = "Loans and receivables to other customers must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 349",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Impairment & Recoveries (MFCR 350, 395, 397) ───────────

            new()
            {
                RuleCode = "BR-047",
                RuleName = "Impairment Must Not Exceed Gross Loans",
                Description = "Total impairment allowance must not exceed the gross loans and receivables.",
                RuleType = "Custom",
                Expression = "total_impairment <= total_gross_loans",
                AppliesToTemplates = "MFCR 350",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-048",
                RuleName = "Classified Account Recoveries Must Not Be Negative",
                Description = "Recoveries from classified accounts must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 395",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-049",
                RuleName = "Fully Impaired Account Recoveries Must Not Be Negative",
                Description = "Recoveries from fully impaired accounts must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 397",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Other Assets & Investments (MFCR 352–362) ──────────────

            new()
            {
                RuleCode = "BR-050",
                RuleName = "Quoted/Unquoted Investments Must Not Be Negative",
                Description = "Other investments (quoted and unquoted) must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 352",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-051",
                RuleName = "Investments in Subsidiaries Must Not Be Negative",
                Description = "Investments in subsidiaries and associates must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 354",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-052",
                RuleName = "Other Assets Must Not Be Negative",
                Description = "Other assets must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 356",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-053",
                RuleName = "Intangible Assets Must Not Be Negative",
                Description = "Intangible assets must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 358",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-054",
                RuleName = "Non-Current Assets Held for Sale Must Not Be Negative",
                Description = "Non-current assets held for sale must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 360",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-055",
                RuleName = "Property Plant & Equipment Must Not Be Negative",
                Description = "Property, plant and equipment (PPE) must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 362",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Liabilities Detail (MFCR 376–388) ──────────────────────

            new()
            {
                RuleCode = "BR-056",
                RuleName = "Contingent Liabilities Must Not Be Negative",
                Description = "Contingent liabilities must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 376",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-057",
                RuleName = "Derivative Financial Liabilities Must Not Be Negative",
                Description = "Derivative financial liabilities must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 385",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-058",
                RuleName = "Other Liabilities Must Not Be Negative",
                Description = "Other liabilities must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 387",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Insider Lending & Statutory (MFCR 1510–1530) ───────────

            new()
            {
                RuleCode = "BR-059",
                RuleName = "Credit to Directors Must Not Be Negative",
                Description = "Credit extended to directors, employees and shareholders must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1510",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-060",
                RuleName = "Director Credit Completeness",
                Description = "All mandatory fields must be provided for insider lending returns.",
                RuleType = "Completeness",
                AppliesToTemplates = "MFCR 1510",
                AppliesToFields = "[\"name_of_director\",\"amount_of_credit\",\"purpose_of_credit\"]",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-061",
                RuleName = "Borrowings from Non-Financial Companies Must Not Be Negative",
                Description = "Borrowings from individuals and non-financial companies must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1530",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Maturity & Sectoral (MFCR 1540–1550) ───────────────────

            new()
            {
                RuleCode = "BR-062",
                RuleName = "Maturity Profile Assets Must Not Be Negative",
                Description = "All financial asset maturity profile values must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1540",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-063",
                RuleName = "Sectoral Credit Must Not Be Negative",
                Description = "Credit by sector and loan type values must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1550",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Staff & HR (MFCR 1590) ─────────────────────────────────

            new()
            {
                RuleCode = "BR-064",
                RuleName = "Dismissed Staff Count Must Not Be Negative",
                Description = "Number of dismissed or terminated staff must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1590",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-065",
                RuleName = "Dismissed Staff Completeness",
                Description = "All required details must be provided for dismissed/terminated staff records.",
                RuleType = "Completeness",
                AppliesToTemplates = "MFCR 1590",
                AppliesToFields = "[\"name_of_staff\",\"designation\",\"reason_for_dismissal\"]",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Government Loans (MFCR 1610–1620) ──────────────────────

            new()
            {
                RuleCode = "BR-066",
                RuleName = "State Government Loans Must Not Be Negative",
                Description = "Loans to state governments and FCT must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1610",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-067",
                RuleName = "Local Government Loans Must Not Be Negative",
                Description = "Loans to local governments must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 1620",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Quarterly Returns (QFCR) ───────────────────────────────

            new()
            {
                RuleCode = "BR-068",
                RuleName = "Direct Credit Substitutes Must Not Be Negative",
                Description = "Direct credit substitutes (guarantees, standby LCs) must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "QFCR 364",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-069",
                RuleName = "Transaction-Related Contingent Items Must Not Be Negative",
                Description = "Transaction-related contingent items must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "QFCR 366",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-070",
                RuleName = "Trade Contingencies Must Not Be Negative",
                Description = "Short-term self-liquidating trade-related contingencies must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "QFCR 368",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-071",
                RuleName = "Forward Asset Purchases Must Not Be Negative",
                Description = "Forward asset purchases, deposits placed and partly paid shares must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "QFCR 370",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-072",
                RuleName = "Borrowings from Banks Must Not Be Negative",
                Description = "Borrowings from banks must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "QFCR 377",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-073",
                RuleName = "Borrowings from Other Finance Companies Must Not Be Negative",
                Description = "Borrowings from other finance companies must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "QFCR 379",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-074",
                RuleName = "Borrowings from Other FIs Must Not Be Negative",
                Description = "Borrowings from other financial institutions must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "QFCR 381",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Semi-Annual Returns (SFCR) ──────────────────────────────

            new()
            {
                RuleCode = "BR-075",
                RuleName = "Investment in Shares Must Not Be Negative",
                Description = "Semi-annual return on investment in shares must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "SFCR 1900",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-076",
                RuleName = "Corporate Profile Completeness",
                Description = "All mandatory fields in the corporate profile return must be populated.",
                RuleType = "Completeness",
                AppliesToTemplates = "SFCR 1910",
                AppliesToFields = "[\"institution_name\",\"date_of_incorporation\",\"rc_number\",\"license_date\"]",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-077",
                RuleName = "Branch Count Must Not Be Negative",
                Description = "Number of branches reported must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total_branches >= 0",
                AppliesToTemplates = "SFCR 1920",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-078",
                RuleName = "Directors Completeness",
                Description = "All required fields for directors return must be populated.",
                RuleType = "Completeness",
                AppliesToTemplates = "SFCR 1930",
                AppliesToFields = "[\"name\",\"designation\",\"nationality\",\"date_of_appointment\"]",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-079",
                RuleName = "Shareholders Completeness",
                Description = "All required fields for shareholders return must be populated.",
                RuleType = "Completeness",
                AppliesToTemplates = "SFCR 1940",
                AppliesToFields = "[\"shareholder_name\",\"number_of_shares\",\"percentage_holding\"]",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-080",
                RuleName = "Shareholding Percentage Must Not Exceed 100",
                Description = "Individual shareholder percentage holding must not exceed 100%.",
                RuleType = "ThresholdCheck",
                Expression = "percentage_holding <= 100",
                AppliesToTemplates = "SFCR 1940",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Cross-Template Consistency & Aggregates ─────────────────

            new()
            {
                RuleCode = "BR-081",
                RuleName = "Net Interest Income Consistency",
                Description = "Net interest income must equal total interest income minus total interest expense.",
                RuleType = "Custom",
                Expression = "net_interest_income = total_interest_income - total_interest_expense",
                AppliesToTemplates = "MFCR 1000",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-082",
                RuleName = "Total Deposits Must Not Be Negative",
                Description = "Total customer deposits must be zero or positive on the balance sheet.",
                RuleType = "ThresholdCheck",
                Expression = "total_deposits >= 0",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-083",
                RuleName = "Paid-Up Capital Must Be Positive",
                Description = "Paid-up share capital must be greater than zero for a licensed institution.",
                RuleType = "ThresholdCheck",
                Expression = "paid_up_capital > 0",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-084",
                RuleName = "Retained Earnings Consistency",
                Description = "Retained earnings should reflect prior period plus current year profit/loss.",
                RuleType = "Custom",
                Expression = "retained_earnings = prior_retained_earnings + profit_or_loss",
                AppliesToTemplates = "MFCR 300",
                Severity = ValidationSeverity.Warning,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Fraud & Complaints Extended ─────────────────────────────

            new()
            {
                RuleCode = "BR-085",
                RuleName = "Fraud Cases Count Must Not Be Negative",
                Description = "Number of fraud and forgery cases reported must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "number_of_cases >= 0",
                AppliesToTemplates = "MFCR 1570",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-086",
                RuleName = "Fraud Report Completeness",
                Description = "All mandatory fraud report fields must be provided.",
                RuleType = "Completeness",
                AppliesToTemplates = "MFCR 1570",
                AppliesToFields = "[\"date_of_fraud\",\"amount_involved\",\"nature_of_fraud\",\"status\"]",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-087",
                RuleName = "Consumer Complaints Count Must Not Be Negative",
                Description = "Total number of consumer complaints must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total_complaints >= 0",
                AppliesToTemplates = "MFCR 1600",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-088",
                RuleName = "Pending Complaints Must Not Be Negative",
                Description = "Number of pending complaints carried forward must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "complaints_pending >= 0",
                AppliesToTemplates = "MFCR 1600",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-089",
                RuleName = "Complaints Balance Check",
                Description = "Total complaints must equal resolved plus pending plus escalated.",
                RuleType = "Custom",
                Expression = "total_complaints = complaints_resolved + complaints_pending + complaints_escalated",
                AppliesToTemplates = "MFCR 1600",
                Severity = ValidationSeverity.Warning,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Placements & Certificates of Deposit (MFCR 312, 334) ───

            new()
            {
                RuleCode = "BR-090",
                RuleName = "Secured Placements with Discount Houses Must Not Be Negative",
                Description = "Secured placements with discount houses must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 312",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-091",
                RuleName = "Certificates of Deposit Held Must Not Be Negative",
                Description = "Certificates of deposit held must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 334",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-092",
                RuleName = "Commercial Papers Must Not Be Negative",
                Description = "Commercial papers holdings must be zero or positive.",
                RuleType = "ThresholdCheck",
                Expression = "total >= 0",
                AppliesToTemplates = "MFCR 336",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Quarterly Statistical Returns (MFCR 1630) ──────────────

            new()
            {
                RuleCode = "BR-093",
                RuleName = "Quarterly Statistical Return Completeness",
                Description = "All mandatory fields in the quarterly statistical return must be populated.",
                RuleType = "Completeness",
                AppliesToTemplates = "MFCR 1630",
                Severity = ValidationSeverity.Warning,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Management & Officers (SFCR 1950–1960) ──────────────────

            new()
            {
                RuleCode = "BR-094",
                RuleName = "Management Officers Completeness",
                Description = "All required fields for management and top officers return must be populated.",
                RuleType = "Completeness",
                AppliesToTemplates = "SFCR 1950",
                AppliesToFields = "[\"name\",\"designation\",\"qualification\",\"date_of_appointment\"]",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-095",
                RuleName = "Branch Closure Completeness",
                Description = "All required fields for branch closure records must be populated.",
                RuleType = "Completeness",
                AppliesToTemplates = "SFCR 1960",
                AppliesToFields = "[\"branch_name\",\"branch_address\",\"date_of_closure\",\"reason_for_closure\"]",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },

            // ── Global / Cross-Template Rules ───────────────────────────

            new()
            {
                RuleCode = "BR-096",
                RuleName = "No Blank Numeric Fields in Submitted Returns",
                Description = "All numeric fields must have an explicit value (zero is acceptable) rather than being left blank.",
                RuleType = "Completeness",
                AppliesToTemplates = "*",
                Severity = ValidationSeverity.Warning,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-097",
                RuleName = "Reporting Period Consistency",
                Description = "The reporting period end date must not precede the start date.",
                RuleType = "DateCheck",
                AppliesToTemplates = "*",
                AppliesToFields = "[\"period_start_date\",\"period_end_date\"]",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-098",
                RuleName = "Negative Value Alert on All Returns",
                Description = "Flag any numeric field that has a negative value where negative is not expected by CBN standards.",
                RuleType = "ThresholdCheck",
                Expression = "value >= 0",
                AppliesToTemplates = "*",
                Severity = ValidationSeverity.Warning,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-099",
                RuleName = "Submission Must Have Institution Code",
                Description = "Every submitted return must have a valid institution code associated with it.",
                RuleType = "Completeness",
                AppliesToTemplates = "*",
                AppliesToFields = "[\"institution_code\"]",
                Severity = ValidationSeverity.Error,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            },
            new()
            {
                RuleCode = "BR-100",
                RuleName = "Large Value Transaction Alert",
                Description = "Flag any single transaction or line item exceeding NGN 10 billion for supervisory review.",
                RuleType = "ThresholdCheck",
                Expression = "value <= 10000000000",
                AppliesToTemplates = "*",
                Severity = ValidationSeverity.Warning,
                IsActive = true, CreatedAt = now, CreatedBy = seeder
            }
        ];
    }
}
