using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Returns;
using FC.Engine.Domain.Returns.FixedRow;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Persistence.Repositories;

public class ReturnRepository : IReturnRepository
{
    private readonly FcEngineDbContext _db;

    public ReturnRepository(FcEngineDbContext db)
    {
        _db = db;
    }

    public async Task Save(IReturnData data, int submissionId, CancellationToken ct = default)
    {
        switch (data)
        {
            case Mfcr300Data mfcr300:
                await SaveMfcr300(mfcr300, submissionId, ct);
                break;
            default:
                throw new NotSupportedException($"Return type {data.GetType().Name} is not yet supported for persistence.");
        }
    }

    public async Task<IReturnData?> GetBySubmissionPeriod(
        int institutionId, int returnPeriodId, ReturnCode returnCode, CancellationToken ct = default)
    {
        var tableName = returnCode.ToTableName();

        return tableName switch
        {
            "mfcr_300" => await GetMfcr300ByPeriod(institutionId, returnPeriodId, ct),
            _ => null
        };
    }

    public async Task<IReturnData?> GetBySubmissionId(int submissionId, CancellationToken ct = default)
    {
        var entity = await _db.Mfcr300.FirstOrDefaultAsync(e => e.SubmissionId == submissionId, ct);
        return entity != null ? MapToMfcr300Data(entity) : null;
    }

    private async Task SaveMfcr300(Mfcr300Data data, int submissionId, CancellationToken ct)
    {
        var entity = new Mfcr300Entity
        {
            SubmissionId = submissionId,
            CashNotes = data.CashNotes,
            CashCoins = data.CashCoins,
            TotalCash = data.TotalCash,
            DueFromBanksNigeria = data.DueFromBanksNigeria,
            UnclearedEffects = data.UnclearedEffects,
            DueFromOtherFi = data.DueFromOtherFi,
            TotalDueFromBanksNigeria = data.TotalDueFromBanksNigeria,
            DueFromBanksOecd = data.DueFromBanksOecd,
            DueFromBanksNonOecd = data.DueFromBanksNonOecd,
            TotalDueFromBanksOutside = data.TotalDueFromBanksOutside,
            MoneyAtCallSecured = data.MoneyAtCallSecured,
            MoneyAtCallUnsecured = data.MoneyAtCallUnsecured,
            TotalMoneyAtCall = data.TotalMoneyAtCall,
            PlacementsSecuredBanks = data.PlacementsSecuredBanks,
            PlacementsUnsecuredBanks = data.PlacementsUnsecuredBanks,
            PlacementsDiscountHouses = data.PlacementsDiscountHouses,
            TotalBankPlacements = data.TotalBankPlacements,
            DerivativeFinancialAssets = data.DerivativeFinancialAssets,
            TreasuryBills = data.TreasuryBills,
            FgnBonds = data.FgnBonds,
            StateGovtBonds = data.StateGovtBonds,
            LocalGovtBonds = data.LocalGovtBonds,
            CorporateBonds = data.CorporateBonds,
            OtherBonds = data.OtherBonds,
            TreasuryCertificates = data.TreasuryCertificates,
            CbnRegisteredCertificates = data.CbnRegisteredCertificates,
            CertificatesOfDeposit = data.CertificatesOfDeposit,
            CommercialPapers = data.CommercialPapers,
            TotalSecurities = data.TotalSecurities,
            LoansToFiNigeria = data.LoansToFiNigeria,
            LoansToSubsidiaryNigeria = data.LoansToSubsidiaryNigeria,
            LoansToSubsidiaryOutside = data.LoansToSubsidiaryOutside,
            LoansToAssociateNigeria = data.LoansToAssociateNigeria,
            LoansToAssociateOutside = data.LoansToAssociateOutside,
            LoansToOtherEntitiesOutside = data.LoansToOtherEntitiesOutside,
            LoansToGovernment = data.LoansToGovernment,
            LoansToOtherCustomers = data.LoansToOtherCustomers,
            TotalGrossLoans = data.TotalGrossLoans,
            ImpairmentOnLoans = data.ImpairmentOnLoans,
            TotalNetLoans = data.TotalNetLoans,
            OtherInvestmentsQuoted = data.OtherInvestmentsQuoted,
            OtherInvestmentsUnquoted = data.OtherInvestmentsUnquoted,
            InvestmentsInSubsidiaries = data.InvestmentsInSubsidiaries,
            InvestmentsInAssociates = data.InvestmentsInAssociates,
            OtherAssets = data.OtherAssets,
            IntangibleAssets = data.IntangibleAssets,
            NonCurrentAssetsHeldForSale = data.NonCurrentAssetsHeldForSale,
            PropertyPlantEquipment = data.PropertyPlantEquipment,
            TotalAssets = data.TotalAssets,
            BorrowingsFromBanks = data.BorrowingsFromBanks,
            BorrowingsFromOtherFc = data.BorrowingsFromOtherFc,
            BorrowingsFromOtherFi = data.BorrowingsFromOtherFi,
            BorrowingsFromIndividuals = data.BorrowingsFromIndividuals,
            TotalBorrowings = data.TotalBorrowings,
            DerivativeFinancialLiabilities = data.DerivativeFinancialLiabilities,
            OtherLiabilities = data.OtherLiabilities,
            TotalLiabilities = data.TotalLiabilities,
            PaidUpCapital = data.PaidUpCapital,
            SharePremium = data.SharePremium,
            RetainedEarnings = data.RetainedEarnings,
            StatutoryReserve = data.StatutoryReserve,
            OtherReserves = data.OtherReserves,
            RevaluationReserve = data.RevaluationReserve,
            MinorityInterest = data.MinorityInterest,
            TotalEquity = data.TotalEquity,
            TotalLiabilitiesAndEquity = data.TotalLiabilitiesAndEquity,
            CreatedAt = DateTime.UtcNow
        };

        _db.Mfcr300.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Mfcr300Data?> GetMfcr300ByPeriod(int institutionId, int returnPeriodId, CancellationToken ct)
    {
        var entity = await _db.Mfcr300
            .Join(_db.Submissions,
                m => m.SubmissionId,
                s => s.Id,
                (m, s) => new { Return = m, Submission = s })
            .Where(x => x.Submission.InstitutionId == institutionId
                     && x.Submission.ReturnPeriodId == returnPeriodId)
            .Select(x => x.Return)
            .FirstOrDefaultAsync(ct);

        return entity != null ? MapToMfcr300Data(entity) : null;
    }

    private static Mfcr300Data MapToMfcr300Data(Mfcr300Entity e)
    {
        return new Mfcr300Data
        {
            CashNotes = e.CashNotes,
            CashCoins = e.CashCoins,
            TotalCash = e.TotalCash,
            DueFromBanksNigeria = e.DueFromBanksNigeria,
            UnclearedEffects = e.UnclearedEffects,
            DueFromOtherFi = e.DueFromOtherFi,
            TotalDueFromBanksNigeria = e.TotalDueFromBanksNigeria,
            DueFromBanksOecd = e.DueFromBanksOecd,
            DueFromBanksNonOecd = e.DueFromBanksNonOecd,
            TotalDueFromBanksOutside = e.TotalDueFromBanksOutside,
            MoneyAtCallSecured = e.MoneyAtCallSecured,
            MoneyAtCallUnsecured = e.MoneyAtCallUnsecured,
            TotalMoneyAtCall = e.TotalMoneyAtCall,
            PlacementsSecuredBanks = e.PlacementsSecuredBanks,
            PlacementsUnsecuredBanks = e.PlacementsUnsecuredBanks,
            PlacementsDiscountHouses = e.PlacementsDiscountHouses,
            TotalBankPlacements = e.TotalBankPlacements,
            DerivativeFinancialAssets = e.DerivativeFinancialAssets,
            TreasuryBills = e.TreasuryBills,
            FgnBonds = e.FgnBonds,
            StateGovtBonds = e.StateGovtBonds,
            LocalGovtBonds = e.LocalGovtBonds,
            CorporateBonds = e.CorporateBonds,
            OtherBonds = e.OtherBonds,
            TreasuryCertificates = e.TreasuryCertificates,
            CbnRegisteredCertificates = e.CbnRegisteredCertificates,
            CertificatesOfDeposit = e.CertificatesOfDeposit,
            CommercialPapers = e.CommercialPapers,
            TotalSecurities = e.TotalSecurities,
            LoansToFiNigeria = e.LoansToFiNigeria,
            LoansToSubsidiaryNigeria = e.LoansToSubsidiaryNigeria,
            LoansToSubsidiaryOutside = e.LoansToSubsidiaryOutside,
            LoansToAssociateNigeria = e.LoansToAssociateNigeria,
            LoansToAssociateOutside = e.LoansToAssociateOutside,
            LoansToOtherEntitiesOutside = e.LoansToOtherEntitiesOutside,
            LoansToGovernment = e.LoansToGovernment,
            LoansToOtherCustomers = e.LoansToOtherCustomers,
            TotalGrossLoans = e.TotalGrossLoans,
            ImpairmentOnLoans = e.ImpairmentOnLoans,
            TotalNetLoans = e.TotalNetLoans,
            OtherInvestmentsQuoted = e.OtherInvestmentsQuoted,
            OtherInvestmentsUnquoted = e.OtherInvestmentsUnquoted,
            InvestmentsInSubsidiaries = e.InvestmentsInSubsidiaries,
            InvestmentsInAssociates = e.InvestmentsInAssociates,
            OtherAssets = e.OtherAssets,
            IntangibleAssets = e.IntangibleAssets,
            NonCurrentAssetsHeldForSale = e.NonCurrentAssetsHeldForSale,
            PropertyPlantEquipment = e.PropertyPlantEquipment,
            TotalAssets = e.TotalAssets,
            BorrowingsFromBanks = e.BorrowingsFromBanks,
            BorrowingsFromOtherFc = e.BorrowingsFromOtherFc,
            BorrowingsFromOtherFi = e.BorrowingsFromOtherFi,
            BorrowingsFromIndividuals = e.BorrowingsFromIndividuals,
            TotalBorrowings = e.TotalBorrowings,
            DerivativeFinancialLiabilities = e.DerivativeFinancialLiabilities,
            OtherLiabilities = e.OtherLiabilities,
            TotalLiabilities = e.TotalLiabilities,
            PaidUpCapital = e.PaidUpCapital,
            SharePremium = e.SharePremium,
            RetainedEarnings = e.RetainedEarnings,
            StatutoryReserve = e.StatutoryReserve,
            OtherReserves = e.OtherReserves,
            RevaluationReserve = e.RevaluationReserve,
            MinorityInterest = e.MinorityInterest,
            TotalEquity = e.TotalEquity,
            TotalLiabilitiesAndEquity = e.TotalLiabilitiesAndEquity
        };
    }
}
