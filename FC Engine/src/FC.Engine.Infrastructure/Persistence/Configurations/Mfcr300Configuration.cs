using FC.Engine.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FC.Engine.Infrastructure.Persistence.Configurations;

public class Mfcr300Configuration : IEntityTypeConfiguration<Mfcr300Entity>
{
    public void Configure(EntityTypeBuilder<Mfcr300Entity> builder)
    {
        builder.ToTable("mfcr_300");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.SubmissionId).IsRequired();

        // All monetary columns: DECIMAL(20,2)
        ConfigureMoneyColumn(builder, e => e.CashNotes, "cash_notes");
        ConfigureMoneyColumn(builder, e => e.CashCoins, "cash_coins");
        ConfigureMoneyColumn(builder, e => e.TotalCash, "total_cash");
        ConfigureMoneyColumn(builder, e => e.DueFromBanksNigeria, "due_from_banks_nigeria");
        ConfigureMoneyColumn(builder, e => e.UnclearedEffects, "uncleared_effects");
        ConfigureMoneyColumn(builder, e => e.DueFromOtherFi, "due_from_other_fi");
        ConfigureMoneyColumn(builder, e => e.TotalDueFromBanksNigeria, "total_due_from_banks_nigeria");
        ConfigureMoneyColumn(builder, e => e.DueFromBanksOecd, "due_from_banks_oecd");
        ConfigureMoneyColumn(builder, e => e.DueFromBanksNonOecd, "due_from_banks_non_oecd");
        ConfigureMoneyColumn(builder, e => e.TotalDueFromBanksOutside, "total_due_from_banks_outside");
        ConfigureMoneyColumn(builder, e => e.MoneyAtCallSecured, "money_at_call_secured");
        ConfigureMoneyColumn(builder, e => e.MoneyAtCallUnsecured, "money_at_call_unsecured");
        ConfigureMoneyColumn(builder, e => e.TotalMoneyAtCall, "total_money_at_call");
        ConfigureMoneyColumn(builder, e => e.PlacementsSecuredBanks, "placements_secured_banks");
        ConfigureMoneyColumn(builder, e => e.PlacementsUnsecuredBanks, "placements_unsecured_banks");
        ConfigureMoneyColumn(builder, e => e.PlacementsDiscountHouses, "placements_discount_houses");
        ConfigureMoneyColumn(builder, e => e.TotalBankPlacements, "total_bank_placements");
        ConfigureMoneyColumn(builder, e => e.DerivativeFinancialAssets, "derivative_financial_assets");
        ConfigureMoneyColumn(builder, e => e.TreasuryBills, "treasury_bills");
        ConfigureMoneyColumn(builder, e => e.FgnBonds, "fgn_bonds");
        ConfigureMoneyColumn(builder, e => e.StateGovtBonds, "state_govt_bonds");
        ConfigureMoneyColumn(builder, e => e.LocalGovtBonds, "local_govt_bonds");
        ConfigureMoneyColumn(builder, e => e.CorporateBonds, "corporate_bonds");
        ConfigureMoneyColumn(builder, e => e.OtherBonds, "other_bonds");
        ConfigureMoneyColumn(builder, e => e.TreasuryCertificates, "treasury_certificates");
        ConfigureMoneyColumn(builder, e => e.CbnRegisteredCertificates, "cbn_registered_certificates");
        ConfigureMoneyColumn(builder, e => e.CertificatesOfDeposit, "certificates_of_deposit");
        ConfigureMoneyColumn(builder, e => e.CommercialPapers, "commercial_papers");
        ConfigureMoneyColumn(builder, e => e.TotalSecurities, "total_securities");
        ConfigureMoneyColumn(builder, e => e.LoansToFiNigeria, "loans_to_fi_nigeria");
        ConfigureMoneyColumn(builder, e => e.LoansToSubsidiaryNigeria, "loans_to_subsidiary_nigeria");
        ConfigureMoneyColumn(builder, e => e.LoansToSubsidiaryOutside, "loans_to_subsidiary_outside");
        ConfigureMoneyColumn(builder, e => e.LoansToAssociateNigeria, "loans_to_associate_nigeria");
        ConfigureMoneyColumn(builder, e => e.LoansToAssociateOutside, "loans_to_associate_outside");
        ConfigureMoneyColumn(builder, e => e.LoansToOtherEntitiesOutside, "loans_to_other_entities_outside");
        ConfigureMoneyColumn(builder, e => e.LoansToGovernment, "loans_to_government");
        ConfigureMoneyColumn(builder, e => e.LoansToOtherCustomers, "loans_to_other_customers");
        ConfigureMoneyColumn(builder, e => e.TotalGrossLoans, "total_gross_loans");
        ConfigureMoneyColumn(builder, e => e.ImpairmentOnLoans, "impairment_on_loans");
        ConfigureMoneyColumn(builder, e => e.TotalNetLoans, "total_net_loans");
        ConfigureMoneyColumn(builder, e => e.OtherInvestmentsQuoted, "other_investments_quoted");
        ConfigureMoneyColumn(builder, e => e.OtherInvestmentsUnquoted, "other_investments_unquoted");
        ConfigureMoneyColumn(builder, e => e.InvestmentsInSubsidiaries, "investments_in_subsidiaries");
        ConfigureMoneyColumn(builder, e => e.InvestmentsInAssociates, "investments_in_associates");
        ConfigureMoneyColumn(builder, e => e.OtherAssets, "other_assets");
        ConfigureMoneyColumn(builder, e => e.IntangibleAssets, "intangible_assets");
        ConfigureMoneyColumn(builder, e => e.NonCurrentAssetsHeldForSale, "non_current_assets_held_for_sale");
        ConfigureMoneyColumn(builder, e => e.PropertyPlantEquipment, "property_plant_equipment");
        ConfigureMoneyColumn(builder, e => e.TotalAssets, "total_assets");
        ConfigureMoneyColumn(builder, e => e.BorrowingsFromBanks, "borrowings_from_banks");
        ConfigureMoneyColumn(builder, e => e.BorrowingsFromOtherFc, "borrowings_from_other_fc");
        ConfigureMoneyColumn(builder, e => e.BorrowingsFromOtherFi, "borrowings_from_other_fi");
        ConfigureMoneyColumn(builder, e => e.BorrowingsFromIndividuals, "borrowings_from_individuals");
        ConfigureMoneyColumn(builder, e => e.TotalBorrowings, "total_borrowings");
        ConfigureMoneyColumn(builder, e => e.DerivativeFinancialLiabilities, "derivative_financial_liabilities");
        ConfigureMoneyColumn(builder, e => e.OtherLiabilities, "other_liabilities");
        ConfigureMoneyColumn(builder, e => e.TotalLiabilities, "total_liabilities");
        ConfigureMoneyColumn(builder, e => e.PaidUpCapital, "paid_up_capital");
        ConfigureMoneyColumn(builder, e => e.SharePremium, "share_premium");
        ConfigureMoneyColumn(builder, e => e.RetainedEarnings, "retained_earnings");
        ConfigureMoneyColumn(builder, e => e.StatutoryReserve, "statutory_reserve");
        ConfigureMoneyColumn(builder, e => e.OtherReserves, "other_reserves");
        ConfigureMoneyColumn(builder, e => e.RevaluationReserve, "revaluation_reserve");
        ConfigureMoneyColumn(builder, e => e.MinorityInterest, "minority_interest");
        ConfigureMoneyColumn(builder, e => e.TotalEquity, "total_equity");
        ConfigureMoneyColumn(builder, e => e.TotalLiabilitiesAndEquity, "total_liabilities_and_equity");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(e => e.SubmissionId);
    }

    private static void ConfigureMoneyColumn(
        EntityTypeBuilder<Mfcr300Entity> builder,
        System.Linq.Expressions.Expression<Func<Mfcr300Entity, decimal?>> propertyExpression,
        string columnName)
    {
        builder.Property(propertyExpression)
            .HasColumnName(columnName)
            .HasColumnType("decimal(20,2)");
    }
}
