using System.Xml.Linq;
using FC.Engine.Domain.Returns;
using FC.Engine.Domain.Returns.FixedRow;

namespace FC.Engine.Infrastructure.Xml.Parsers;

public class Mfcr300XmlParser : IReturnXmlParser
{
    private static readonly XNamespace Ns = "urn:cbn:dfis:fc:mfcr300";

    public string ReturnCodeValue => "MFCR 300";

    public IReturnData Parse(Stream xml)
    {
        var doc = XDocument.Load(xml);
        var root = doc.Root!;
        var fp = root.Element(Ns + "FinancialPosition")!;

        return new Mfcr300Data
        {
            CashNotes = GetDecimal(fp, "CashNotes"),
            CashCoins = GetDecimal(fp, "CashCoins"),
            TotalCash = GetDecimal(fp, "TotalCash"),
            DueFromBanksNigeria = GetDecimal(fp, "DueFromBanksNigeria"),
            UnclearedEffects = GetDecimal(fp, "UnclearedEffects"),
            DueFromOtherFi = GetDecimal(fp, "DueFromOtherFi"),
            TotalDueFromBanksNigeria = GetDecimal(fp, "TotalDueFromBanksNigeria"),
            DueFromBanksOecd = GetDecimal(fp, "DueFromBanksOecd"),
            DueFromBanksNonOecd = GetDecimal(fp, "DueFromBanksNonOecd"),
            TotalDueFromBanksOutside = GetDecimal(fp, "TotalDueFromBanksOutside"),
            MoneyAtCallSecured = GetDecimal(fp, "MoneyAtCallSecured"),
            MoneyAtCallUnsecured = GetDecimal(fp, "MoneyAtCallUnsecured"),
            TotalMoneyAtCall = GetDecimal(fp, "TotalMoneyAtCall"),
            PlacementsSecuredBanks = GetDecimal(fp, "PlacementsSecuredBanks"),
            PlacementsUnsecuredBanks = GetDecimal(fp, "PlacementsUnsecuredBanks"),
            PlacementsDiscountHouses = GetDecimal(fp, "PlacementsDiscountHouses"),
            TotalBankPlacements = GetDecimal(fp, "TotalBankPlacements"),
            DerivativeFinancialAssets = GetDecimal(fp, "DerivativeFinancialAssets"),
            TreasuryBills = GetDecimal(fp, "TreasuryBills"),
            FgnBonds = GetDecimal(fp, "FgnBonds"),
            StateGovtBonds = GetDecimal(fp, "StateGovtBonds"),
            LocalGovtBonds = GetDecimal(fp, "LocalGovtBonds"),
            CorporateBonds = GetDecimal(fp, "CorporateBonds"),
            OtherBonds = GetDecimal(fp, "OtherBonds"),
            TreasuryCertificates = GetDecimal(fp, "TreasuryCertificates"),
            CbnRegisteredCertificates = GetDecimal(fp, "CbnRegisteredCertificates"),
            CertificatesOfDeposit = GetDecimal(fp, "CertificatesOfDeposit"),
            CommercialPapers = GetDecimal(fp, "CommercialPapers"),
            TotalSecurities = GetDecimal(fp, "TotalSecurities"),
            LoansToFiNigeria = GetDecimal(fp, "LoansToFiNigeria"),
            LoansToSubsidiaryNigeria = GetDecimal(fp, "LoansToSubsidiaryNigeria"),
            LoansToSubsidiaryOutside = GetDecimal(fp, "LoansToSubsidiaryOutside"),
            LoansToAssociateNigeria = GetDecimal(fp, "LoansToAssociateNigeria"),
            LoansToAssociateOutside = GetDecimal(fp, "LoansToAssociateOutside"),
            LoansToOtherEntitiesOutside = GetDecimal(fp, "LoansToOtherEntitiesOutside"),
            LoansToGovernment = GetDecimal(fp, "LoansToGovernment"),
            LoansToOtherCustomers = GetDecimal(fp, "LoansToOtherCustomers"),
            TotalGrossLoans = GetDecimal(fp, "TotalGrossLoans"),
            ImpairmentOnLoans = GetDecimal(fp, "ImpairmentOnLoans"),
            TotalNetLoans = GetDecimal(fp, "TotalNetLoans"),
            OtherInvestmentsQuoted = GetDecimal(fp, "OtherInvestmentsQuoted"),
            OtherInvestmentsUnquoted = GetDecimal(fp, "OtherInvestmentsUnquoted"),
            InvestmentsInSubsidiaries = GetDecimal(fp, "InvestmentsInSubsidiaries"),
            InvestmentsInAssociates = GetDecimal(fp, "InvestmentsInAssociates"),
            OtherAssets = GetDecimal(fp, "OtherAssets"),
            IntangibleAssets = GetDecimal(fp, "IntangibleAssets"),
            NonCurrentAssetsHeldForSale = GetDecimal(fp, "NonCurrentAssetsHeldForSale"),
            PropertyPlantEquipment = GetDecimal(fp, "PropertyPlantEquipment"),
            TotalAssets = GetDecimal(fp, "TotalAssets"),
            BorrowingsFromBanks = GetDecimal(fp, "BorrowingsFromBanks"),
            BorrowingsFromOtherFc = GetDecimal(fp, "BorrowingsFromOtherFc"),
            BorrowingsFromOtherFi = GetDecimal(fp, "BorrowingsFromOtherFi"),
            BorrowingsFromIndividuals = GetDecimal(fp, "BorrowingsFromIndividuals"),
            TotalBorrowings = GetDecimal(fp, "TotalBorrowings"),
            DerivativeFinancialLiabilities = GetDecimal(fp, "DerivativeFinancialLiabilities"),
            OtherLiabilities = GetDecimal(fp, "OtherLiabilities"),
            TotalLiabilities = GetDecimal(fp, "TotalLiabilities"),
            PaidUpCapital = GetDecimal(fp, "PaidUpCapital"),
            SharePremium = GetDecimal(fp, "SharePremium"),
            RetainedEarnings = GetDecimal(fp, "RetainedEarnings"),
            StatutoryReserve = GetDecimal(fp, "StatutoryReserve"),
            OtherReserves = GetDecimal(fp, "OtherReserves"),
            RevaluationReserve = GetDecimal(fp, "RevaluationReserve"),
            MinorityInterest = GetDecimal(fp, "MinorityInterest"),
            TotalEquity = GetDecimal(fp, "TotalEquity"),
            TotalLiabilitiesAndEquity = GetDecimal(fp, "TotalLiabilitiesAndEquity")
        };
    }

    private static decimal? GetDecimal(XElement parent, string elementName)
    {
        var el = parent.Element(Ns + elementName);
        if (el == null || string.IsNullOrWhiteSpace(el.Value))
            return null;
        return decimal.TryParse(el.Value, out var val) ? val : null;
    }
}
