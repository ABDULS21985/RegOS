namespace FC.Engine.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the mfcr_300 table.
/// Maps 1:1 to the SQL schema columns.
/// </summary>
public class Mfcr300Entity
{
    public int Id { get; set; }
    public int SubmissionId { get; set; }

    // Cash
    public decimal? CashNotes { get; set; }
    public decimal? CashCoins { get; set; }
    public decimal? TotalCash { get; set; }

    // Due From Banks Nigeria
    public decimal? DueFromBanksNigeria { get; set; }
    public decimal? UnclearedEffects { get; set; }
    public decimal? DueFromOtherFi { get; set; }
    public decimal? TotalDueFromBanksNigeria { get; set; }

    // Due From Banks Outside
    public decimal? DueFromBanksOecd { get; set; }
    public decimal? DueFromBanksNonOecd { get; set; }
    public decimal? TotalDueFromBanksOutside { get; set; }

    // Money at Call
    public decimal? MoneyAtCallSecured { get; set; }
    public decimal? MoneyAtCallUnsecured { get; set; }
    public decimal? TotalMoneyAtCall { get; set; }

    // Bank Placements
    public decimal? PlacementsSecuredBanks { get; set; }
    public decimal? PlacementsUnsecuredBanks { get; set; }
    public decimal? PlacementsDiscountHouses { get; set; }
    public decimal? TotalBankPlacements { get; set; }

    // Derivative Financial Assets
    public decimal? DerivativeFinancialAssets { get; set; }

    // Securities
    public decimal? TreasuryBills { get; set; }
    public decimal? FgnBonds { get; set; }
    public decimal? StateGovtBonds { get; set; }
    public decimal? LocalGovtBonds { get; set; }
    public decimal? CorporateBonds { get; set; }
    public decimal? OtherBonds { get; set; }
    public decimal? TreasuryCertificates { get; set; }
    public decimal? CbnRegisteredCertificates { get; set; }
    public decimal? CertificatesOfDeposit { get; set; }
    public decimal? CommercialPapers { get; set; }
    public decimal? TotalSecurities { get; set; }

    // Loans
    public decimal? LoansToFiNigeria { get; set; }
    public decimal? LoansToSubsidiaryNigeria { get; set; }
    public decimal? LoansToSubsidiaryOutside { get; set; }
    public decimal? LoansToAssociateNigeria { get; set; }
    public decimal? LoansToAssociateOutside { get; set; }
    public decimal? LoansToOtherEntitiesOutside { get; set; }
    public decimal? LoansToGovernment { get; set; }
    public decimal? LoansToOtherCustomers { get; set; }
    public decimal? TotalGrossLoans { get; set; }
    public decimal? ImpairmentOnLoans { get; set; }
    public decimal? TotalNetLoans { get; set; }

    // Other Investments
    public decimal? OtherInvestmentsQuoted { get; set; }
    public decimal? OtherInvestmentsUnquoted { get; set; }
    public decimal? InvestmentsInSubsidiaries { get; set; }
    public decimal? InvestmentsInAssociates { get; set; }

    // Other Assets
    public decimal? OtherAssets { get; set; }
    public decimal? IntangibleAssets { get; set; }
    public decimal? NonCurrentAssetsHeldForSale { get; set; }
    public decimal? PropertyPlantEquipment { get; set; }
    public decimal? TotalAssets { get; set; }

    // Liabilities
    public decimal? BorrowingsFromBanks { get; set; }
    public decimal? BorrowingsFromOtherFc { get; set; }
    public decimal? BorrowingsFromOtherFi { get; set; }
    public decimal? BorrowingsFromIndividuals { get; set; }
    public decimal? TotalBorrowings { get; set; }
    public decimal? DerivativeFinancialLiabilities { get; set; }
    public decimal? OtherLiabilities { get; set; }
    public decimal? TotalLiabilities { get; set; }

    // Equity
    public decimal? PaidUpCapital { get; set; }
    public decimal? SharePremium { get; set; }
    public decimal? RetainedEarnings { get; set; }
    public decimal? StatutoryReserve { get; set; }
    public decimal? OtherReserves { get; set; }
    public decimal? RevaluationReserve { get; set; }
    public decimal? MinorityInterest { get; set; }
    public decimal? TotalEquity { get; set; }
    public decimal? TotalLiabilitiesAndEquity { get; set; }

    public DateTime CreatedAt { get; set; }
}
