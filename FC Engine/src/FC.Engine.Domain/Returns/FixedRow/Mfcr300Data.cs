using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Domain.Returns.FixedRow;

/// <summary>
/// MFCR 300 - Statement of Financial Position (Balance Sheet).
/// Fixed-row return: one record per submission with ~80 line code fields.
/// </summary>
public class Mfcr300Data : IReturnData
{
    public ReturnCode ReturnCode => ReturnCode.Parse("MFCR 300");

    // FINANCIAL ASSETS: Cash
    [LineCode("10110")] public decimal? CashNotes { get; set; }
    [LineCode("10120")] public decimal? CashCoins { get; set; }
    [LineCode("10140")] public decimal? TotalCash { get; set; }

    // Due From Banks Nigeria
    [LineCode("10170")] public decimal? DueFromBanksNigeria { get; set; }
    [LineCode("10180")] public decimal? UnclearedEffects { get; set; }
    [LineCode("10185")] public decimal? DueFromOtherFi { get; set; }
    [LineCode("10190")] public decimal? TotalDueFromBanksNigeria { get; set; }

    // Due From Banks Outside
    [LineCode("10210")] public decimal? DueFromBanksOecd { get; set; }
    [LineCode("10220")] public decimal? DueFromBanksNonOecd { get; set; }
    [LineCode("10230")] public decimal? TotalDueFromBanksOutside { get; set; }

    // Money at Call
    [LineCode("10250")] public decimal? MoneyAtCallSecured { get; set; }
    [LineCode("10260")] public decimal? MoneyAtCallUnsecured { get; set; }
    [LineCode("10240")] public decimal? TotalMoneyAtCall { get; set; }

    // Bank Placements
    [LineCode("10280")] public decimal? PlacementsSecuredBanks { get; set; }
    [LineCode("10290")] public decimal? PlacementsUnsecuredBanks { get; set; }
    [LineCode("10295")] public decimal? PlacementsDiscountHouses { get; set; }
    [LineCode("10270")] public decimal? TotalBankPlacements { get; set; }

    // Derivative Financial Assets
    [LineCode("10370")] public decimal? DerivativeFinancialAssets { get; set; }

    // Securities
    [LineCode("10380")] public decimal? TreasuryBills { get; set; }
    [LineCode("10390")] public decimal? FgnBonds { get; set; }
    [LineCode("10400")] public decimal? StateGovtBonds { get; set; }
    [LineCode("10410")] public decimal? LocalGovtBonds { get; set; }
    [LineCode("10420")] public decimal? CorporateBonds { get; set; }
    [LineCode("10430")] public decimal? OtherBonds { get; set; }
    [LineCode("10440")] public decimal? TreasuryCertificates { get; set; }
    [LineCode("10450")] public decimal? CbnRegisteredCertificates { get; set; }
    [LineCode("10460")] public decimal? CertificatesOfDeposit { get; set; }
    [LineCode("10470")] public decimal? CommercialPapers { get; set; }
    [LineCode("10480")] public decimal? TotalSecurities { get; set; }

    // Loans and Receivables
    [LineCode("10490")] public decimal? LoansToFiNigeria { get; set; }
    [LineCode("10500")] public decimal? LoansToSubsidiaryNigeria { get; set; }
    [LineCode("10510")] public decimal? LoansToSubsidiaryOutside { get; set; }
    [LineCode("10520")] public decimal? LoansToAssociateNigeria { get; set; }
    [LineCode("10530")] public decimal? LoansToAssociateOutside { get; set; }
    [LineCode("10540")] public decimal? LoansToOtherEntitiesOutside { get; set; }
    [LineCode("10545")] public decimal? LoansToGovernment { get; set; }
    [LineCode("10550")] public decimal? LoansToOtherCustomers { get; set; }
    [LineCode("10560")] public decimal? TotalGrossLoans { get; set; }
    [LineCode("10570")] public decimal? ImpairmentOnLoans { get; set; }
    [LineCode("10580")] public decimal? TotalNetLoans { get; set; }

    // Other Investments
    [LineCode("10590")] public decimal? OtherInvestmentsQuoted { get; set; }
    [LineCode("10600")] public decimal? OtherInvestmentsUnquoted { get; set; }
    [LineCode("10610")] public decimal? InvestmentsInSubsidiaries { get; set; }
    [LineCode("10620")] public decimal? InvestmentsInAssociates { get; set; }

    // Other Assets
    [LineCode("10630")] public decimal? OtherAssets { get; set; }
    [LineCode("10640")] public decimal? IntangibleAssets { get; set; }
    [LineCode("10650")] public decimal? NonCurrentAssetsHeldForSale { get; set; }
    [LineCode("10660")] public decimal? PropertyPlantEquipment { get; set; }
    [LineCode("10670")] public decimal? TotalAssets { get; set; }

    // LIABILITIES: Borrowings
    [LineCode("10680")] public decimal? BorrowingsFromBanks { get; set; }
    [LineCode("10690")] public decimal? BorrowingsFromOtherFc { get; set; }
    [LineCode("10700")] public decimal? BorrowingsFromOtherFi { get; set; }
    [LineCode("10710")] public decimal? BorrowingsFromIndividuals { get; set; }
    [LineCode("10720")] public decimal? TotalBorrowings { get; set; }

    // Other Liabilities
    [LineCode("10730")] public decimal? DerivativeFinancialLiabilities { get; set; }
    [LineCode("10740")] public decimal? OtherLiabilities { get; set; }
    [LineCode("10750")] public decimal? TotalLiabilities { get; set; }

    // EQUITY
    [LineCode("10760")] public decimal? PaidUpCapital { get; set; }
    [LineCode("10770")] public decimal? SharePremium { get; set; }
    [LineCode("10780")] public decimal? RetainedEarnings { get; set; }
    [LineCode("10790")] public decimal? StatutoryReserve { get; set; }
    [LineCode("10800")] public decimal? OtherReserves { get; set; }
    [LineCode("10810")] public decimal? RevaluationReserve { get; set; }
    [LineCode("10820")] public decimal? MinorityInterest { get; set; }
    [LineCode("10830")] public decimal? TotalEquity { get; set; }
    [LineCode("10840")] public decimal? TotalLiabilitiesAndEquity { get; set; }
}
