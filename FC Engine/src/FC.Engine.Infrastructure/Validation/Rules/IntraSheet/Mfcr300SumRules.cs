using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Returns;
using FC.Engine.Domain.Returns.FixedRow;
using FC.Engine.Domain.Validation;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Infrastructure.Validation.Rules.IntraSheet;

public class Mfcr300SumRules : IIntraSheetRule
{
    public string RuleId => "MFCR300_SUMS";
    public ReturnCode ApplicableReturnCode => ReturnCode.Parse("MFCR 300");
    public ValidationCategory Category => ValidationCategory.IntraSheet;
    public ValidationSeverity DefaultSeverity => ValidationSeverity.Error;

    public IEnumerable<ValidationError> Validate(IReturnData data)
    {
        var d = (Mfcr300Data)data;
        var errors = new List<ValidationError>();

        // total_cash = cash_notes + cash_coins
        CheckSum(errors, "total_cash", d.TotalCash, d.CashNotes, d.CashCoins);

        // total_due_from_banks_nigeria = due_from_banks_nigeria + uncleared_effects + due_from_other_fi
        CheckSum(errors, "total_due_from_banks_nigeria", d.TotalDueFromBanksNigeria,
            d.DueFromBanksNigeria, d.UnclearedEffects, d.DueFromOtherFi);

        // total_due_from_banks_outside = due_from_banks_oecd + due_from_banks_non_oecd
        CheckSum(errors, "total_due_from_banks_outside", d.TotalDueFromBanksOutside,
            d.DueFromBanksOecd, d.DueFromBanksNonOecd);

        // total_money_at_call = money_at_call_secured + money_at_call_unsecured
        CheckSum(errors, "total_money_at_call", d.TotalMoneyAtCall,
            d.MoneyAtCallSecured, d.MoneyAtCallUnsecured);

        // total_bank_placements = secured + unsecured + discount_houses
        CheckSum(errors, "total_bank_placements", d.TotalBankPlacements,
            d.PlacementsSecuredBanks, d.PlacementsUnsecuredBanks, d.PlacementsDiscountHouses);

        // total_securities = sum of all security types
        CheckSum(errors, "total_securities", d.TotalSecurities,
            d.TreasuryBills, d.FgnBonds, d.StateGovtBonds, d.LocalGovtBonds,
            d.CorporateBonds, d.OtherBonds, d.TreasuryCertificates,
            d.CbnRegisteredCertificates, d.CertificatesOfDeposit, d.CommercialPapers);

        // total_gross_loans = sum of all loan categories
        CheckSum(errors, "total_gross_loans", d.TotalGrossLoans,
            d.LoansToFiNigeria, d.LoansToSubsidiaryNigeria, d.LoansToSubsidiaryOutside,
            d.LoansToAssociateNigeria, d.LoansToAssociateOutside,
            d.LoansToOtherEntitiesOutside, d.LoansToGovernment, d.LoansToOtherCustomers);

        // total_net_loans = total_gross_loans - impairment_on_loans
        CheckDifference(errors, "total_net_loans", d.TotalNetLoans,
            d.TotalGrossLoans, d.ImpairmentOnLoans);

        // total_assets = sum of all asset categories
        CheckSum(errors, "total_assets", d.TotalAssets,
            d.TotalCash, d.TotalDueFromBanksNigeria, d.TotalDueFromBanksOutside,
            d.TotalMoneyAtCall, d.TotalBankPlacements, d.DerivativeFinancialAssets,
            d.TotalSecurities, d.TotalNetLoans, d.OtherInvestmentsQuoted,
            d.OtherInvestmentsUnquoted, d.InvestmentsInSubsidiaries,
            d.InvestmentsInAssociates, d.OtherAssets, d.IntangibleAssets,
            d.NonCurrentAssetsHeldForSale, d.PropertyPlantEquipment);

        // total_borrowings = sum of all borrowing categories
        CheckSum(errors, "total_borrowings", d.TotalBorrowings,
            d.BorrowingsFromBanks, d.BorrowingsFromOtherFc,
            d.BorrowingsFromOtherFi, d.BorrowingsFromIndividuals);

        // total_liabilities = total_borrowings + derivative_financial_liabilities + other_liabilities
        CheckSum(errors, "total_liabilities", d.TotalLiabilities,
            d.TotalBorrowings, d.DerivativeFinancialLiabilities, d.OtherLiabilities);

        // total_equity = sum of all equity components
        CheckSum(errors, "total_equity", d.TotalEquity,
            d.PaidUpCapital, d.SharePremium, d.RetainedEarnings,
            d.StatutoryReserve, d.OtherReserves, d.RevaluationReserve, d.MinorityInterest);

        // total_liabilities_and_equity = total_liabilities + total_equity
        CheckSum(errors, "total_liabilities_and_equity", d.TotalLiabilitiesAndEquity,
            d.TotalLiabilities, d.TotalEquity);

        // Balance sheet must balance: total_assets == total_liabilities_and_equity
        if (d.TotalAssets.HasValue && d.TotalLiabilitiesAndEquity.HasValue
            && d.TotalAssets.Value != d.TotalLiabilitiesAndEquity.Value)
        {
            errors.Add(new ValidationError
            {
                RuleId = "MFCR300_BALANCE_SHEET",
                Field = "total_assets vs total_liabilities_and_equity",
                Message = "Balance sheet does not balance: Total Assets must equal Total Liabilities and Equity",
                ExpectedValue = d.TotalLiabilitiesAndEquity.Value.ToString("N2"),
                ActualValue = d.TotalAssets.Value.ToString("N2"),
                Severity = ValidationSeverity.Error,
                Category = ValidationCategory.IntraSheet
            });
        }

        return errors;
    }

    private void CheckSum(List<ValidationError> errors, string field,
        decimal? actual, params decimal?[] addends)
    {
        if (!actual.HasValue) return;

        var expected = addends.Where(a => a.HasValue).Sum(a => a!.Value);
        if (actual.Value != expected)
        {
            errors.Add(new ValidationError
            {
                RuleId = $"MFCR300_SUM_{field.ToUpperInvariant()}",
                Field = field,
                Message = $"Sum validation failed for {field}",
                ExpectedValue = expected.ToString("N2"),
                ActualValue = actual.Value.ToString("N2"),
                Severity = DefaultSeverity,
                Category = Category
            });
        }
    }

    private void CheckDifference(List<ValidationError> errors, string field,
        decimal? actual, decimal? minuend, decimal? subtrahend)
    {
        if (!actual.HasValue || !minuend.HasValue) return;

        var expected = minuend.Value - (subtrahend ?? 0m);
        if (actual.Value != expected)
        {
            errors.Add(new ValidationError
            {
                RuleId = $"MFCR300_DIFF_{field.ToUpperInvariant()}",
                Field = field,
                Message = $"Difference validation failed for {field}",
                ExpectedValue = expected.ToString("N2"),
                ActualValue = actual.Value.ToString("N2"),
                Severity = DefaultSeverity,
                Category = Category
            });
        }
    }
}
