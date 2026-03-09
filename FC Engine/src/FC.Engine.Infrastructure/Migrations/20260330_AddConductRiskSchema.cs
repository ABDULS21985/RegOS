using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FC.Engine.Infrastructure.Migrations;

/// <summary>
/// RG-38: Conduct Risk &amp; Market Abuse Surveillance.
/// Creates regulator-owned surveillance tables, whistleblower workflow tables,
/// and threshold parameters with Nigerian regulatory defaults.
/// </summary>
public partial class AddConductRiskSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.SurveillanceRuleParameters', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SurveillanceRuleParameters (
        Id                  INT                 IDENTITY(1,1) PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER    NULL,
        RuleCode            VARCHAR(40)         NOT NULL,
        RegulatorCode       VARCHAR(10)         NOT NULL,
        InstitutionType     VARCHAR(20)         NOT NULL DEFAULT 'ALL',
        ParamName           VARCHAR(60)         NOT NULL,
        ParamValue          DECIMAL(18,6)       NOT NULL,
        Description         NVARCHAR(300)       NULL,
        EffectiveFrom       DATE                NOT NULL DEFAULT CAST(GETUTCDATE() AS DATE),
        IsActive            BIT                 NOT NULL DEFAULT 1,
        CONSTRAINT FK_SurveillanceRuleParameters_Tenant
            FOREIGN KEY (TenantId) REFERENCES dbo.tenants(TenantId),
        CONSTRAINT UQ_SurveillanceRuleParameters
            UNIQUE (RuleCode, RegulatorCode, InstitutionType, ParamName, EffectiveFrom)
    );

    CREATE INDEX IX_SurveillanceRuleParameters_Regulator
        ON dbo.SurveillanceRuleParameters (RegulatorCode, RuleCode, InstitutionType, EffectiveFrom DESC);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.SurveillanceAlerts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SurveillanceAlerts (
        Id                  BIGINT              IDENTITY(1,1) PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
        AlertCode           VARCHAR(40)         NOT NULL,
        RegulatorCode       VARCHAR(10)         NOT NULL,
        InstitutionId       INT                 NULL REFERENCES dbo.institutions(Id),
        Severity            VARCHAR(10)         NOT NULL,
        Category            VARCHAR(20)         NOT NULL,
        Title               NVARCHAR(200)       NOT NULL,
        Detail              NVARCHAR(2000)      NULL,
        EvidenceJson        NVARCHAR(MAX)       NULL,
        PeriodCode          VARCHAR(10)         NULL,
        DetectionRunId      UNIQUEIDENTIFIER    NOT NULL,
        DetectedAt          DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_SurveillanceAlerts_Tenant
        ON dbo.SurveillanceAlerts (TenantId, DetectedAt DESC);
    CREATE INDEX IX_SurveillanceAlerts_Institution
        ON dbo.SurveillanceAlerts (TenantId, InstitutionId, DetectedAt DESC);
    CREATE INDEX IX_SurveillanceAlerts_Category
        ON dbo.SurveillanceAlerts (TenantId, Category, Severity, DetectedAt DESC);
    CREATE INDEX IX_SurveillanceAlerts_Regulator
        ON dbo.SurveillanceAlerts (RegulatorCode, DetectedAt DESC);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.SurveillanceAlertResolutions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SurveillanceAlertResolutions (
        Id                  BIGINT              IDENTITY(1,1) PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
        AlertId             BIGINT              NOT NULL REFERENCES dbo.SurveillanceAlerts(Id),
        RegulatorCode       VARCHAR(10)         NOT NULL,
        ResolvedByUserId    INT                 NOT NULL,
        ResolutionOutcome   VARCHAR(20)         NOT NULL,
        ResolutionNote      NVARCHAR(500)       NOT NULL,
        ResolvedAt          DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_SurveillanceAlertResolutions_Alert
        ON dbo.SurveillanceAlertResolutions (AlertId, ResolvedAt DESC);
    CREATE INDEX IX_SurveillanceAlertResolutions_Tenant
        ON dbo.SurveillanceAlertResolutions (TenantId, ResolvedAt DESC);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.BDCFXTransactions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BDCFXTransactions (
        Id                  BIGINT              IDENTITY(1,1) PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
        InstitutionId       INT                 NOT NULL REFERENCES dbo.institutions(Id),
        RegulatorCode       VARCHAR(10)         NOT NULL,
        TransactionDate     DATE                NOT NULL,
        PeriodCode          VARCHAR(10)         NOT NULL,
        BuyCurrency         VARCHAR(3)          NOT NULL DEFAULT 'USD',
        SellCurrency        VARCHAR(3)          NOT NULL DEFAULT 'NGN',
        BuyRate             DECIMAL(12,4)       NOT NULL,
        SellRate            DECIMAL(12,4)       NOT NULL,
        BuyVolumeUSD        DECIMAL(18,2)       NOT NULL,
        SellVolumeUSD       DECIMAL(18,2)       NOT NULL,
        CBNMidRate          DECIMAL(12,4)       NOT NULL,
        CBNBandUpper        DECIMAL(12,4)       NOT NULL,
        CBNBandLower        DECIMAL(12,4)       NOT NULL,
        CounterpartyId      INT                 NULL REFERENCES dbo.institutions(Id),
        SourceReturnId      BIGINT              NULL,
        CreatedAt           DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_BDCFXTransactions_Entity_Date
            UNIQUE (TenantId, InstitutionId, TransactionDate, BuyCurrency)
    );

    CREATE INDEX IX_BDCFXTransactions_Date
        ON dbo.BDCFXTransactions (TenantId, TransactionDate DESC);
    CREATE INDEX IX_BDCFXTransactions_Regulator
        ON dbo.BDCFXTransactions (RegulatorCode, TransactionDate DESC);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.CorporateAnnouncements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CorporateAnnouncements (
        Id                  BIGINT              IDENTITY(1,1) PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
        RegulatorCode       VARCHAR(10)         NOT NULL,
        SecurityCode        VARCHAR(20)         NOT NULL,
        SecurityName        NVARCHAR(200)       NULL,
        AnnouncementType    VARCHAR(40)         NOT NULL,
        AnnouncementDate    DATE                NOT NULL,
        DisclosureDeadline  DATE                NULL,
        SourceReference     NVARCHAR(300)       NULL,
        CreatedAt           DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_CorporateAnnouncements_Security
        ON dbo.CorporateAnnouncements (TenantId, SecurityCode, AnnouncementDate DESC);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.CMOTradeReports', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CMOTradeReports (
        Id                  BIGINT              IDENTITY(1,1) PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
        InstitutionId       INT                 NOT NULL REFERENCES dbo.institutions(Id),
        RegulatorCode       VARCHAR(10)         NOT NULL,
        InstitutionType     VARCHAR(20)         NOT NULL,
        SecurityCode        VARCHAR(20)         NOT NULL,
        SecurityName        NVARCHAR(200)       NULL,
        TradeDate           DATE                NOT NULL,
        PeriodCode          VARCHAR(10)         NOT NULL,
        TradeType           VARCHAR(10)         NOT NULL,
        Quantity            DECIMAL(18,2)       NOT NULL,
        Price               DECIMAL(14,4)       NOT NULL,
        TradeValueNGN       DECIMAL(18,2)       NOT NULL,
        ClientId            VARCHAR(64)         NULL,
        ReportedAt          DATETIME2(3)        NOT NULL,
        TradeTimestamp      DATETIME2(3)        NOT NULL,
        IsLate              BIT                 NOT NULL DEFAULT 0,
        SourceReturnId      BIGINT              NULL,
        CreatedAt           DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_CMOTradeReports_Security
        ON dbo.CMOTradeReports (TenantId, SecurityCode, TradeDate DESC);
    CREATE INDEX IX_CMOTradeReports_Institution
        ON dbo.CMOTradeReports (TenantId, InstitutionId, TradeDate DESC);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.AMLConductMetrics', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AMLConductMetrics (
        Id                      BIGINT              IDENTITY(1,1) PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
        InstitutionId           INT                 NOT NULL REFERENCES dbo.institutions(Id),
        RegulatorCode           VARCHAR(10)         NOT NULL,
        InstitutionType         VARCHAR(20)         NOT NULL,
        PeriodCode              VARCHAR(10)         NOT NULL,
        AsOfDate                DATE                NOT NULL,
        STRFilingCount          INT                 NOT NULL DEFAULT 0,
        CTRFilingCount          INT                 NOT NULL DEFAULT 0,
        PeerAvgSTRCount         DECIMAL(8,2)        NULL,
        STRDeviation            DECIMAL(8,4)        NULL,
        StructuringAlertCount   INT                 NOT NULL DEFAULT 0,
        PEPAccountCount         INT                 NOT NULL DEFAULT 0,
        PEPFlaggedActivityCount INT                 NOT NULL DEFAULT 0,
        TFSScreeningCount       INT                 NOT NULL DEFAULT 0,
        TFSFalsePositiveRate    DECIMAL(8,4)        NULL,
        TFSTruePositiveCount    INT                 NOT NULL DEFAULT 0,
        CustomerComplaintCount  INT                 NOT NULL DEFAULT 0,
        ComplaintResolutionRate DECIMAL(8,4)        NULL,
        SourceReturnId          BIGINT              NULL,
        CreatedAt               DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_AMLConductMetrics_Entity_Period
            UNIQUE (TenantId, InstitutionId, PeriodCode)
    );

    CREATE INDEX IX_AMLConductMetrics_Regulator
        ON dbo.AMLConductMetrics (RegulatorCode, AsOfDate DESC);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.InsuranceConductMetrics', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InsuranceConductMetrics (
        Id                          BIGINT              IDENTITY(1,1) PRIMARY KEY,
        TenantId                    UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
        InstitutionId               INT                 NOT NULL REFERENCES dbo.institutions(Id),
        RegulatorCode               VARCHAR(10)         NOT NULL,
        InstitutionType             VARCHAR(20)         NOT NULL DEFAULT 'INSURER',
        PeriodCode                  VARCHAR(10)         NOT NULL,
        AsOfDate                    DATE                NOT NULL,
        GrossClaimsNGN              DECIMAL(18,2)       NULL,
        GrossPremiumNGN             DECIMAL(18,2)       NULL,
        ClaimsRatio                 DECIMAL(8,4)        NULL,
        PeerAvgClaimsRatio          DECIMAL(8,4)        NULL,
        GrossPremiumReported        DECIMAL(18,2)       NULL,
        GrossPremiumExpected        DECIMAL(18,2)       NULL,
        PremiumUnderReportingGap    DECIMAL(18,2)       NULL,
        ReinsuranceRecoveries       DECIMAL(18,2)       NULL,
        RelatedPartyReinsurancePct  DECIMAL(8,4)        NULL,
        ComplaintCount              INT                 NOT NULL DEFAULT 0,
        ClaimsDenialRate            DECIMAL(8,4)        NULL,
        SourceReturnId              BIGINT              NULL,
        CreatedAt                   DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_InsuranceConductMetrics_Entity_Period
            UNIQUE (TenantId, InstitutionId, PeriodCode)
    );

    CREATE INDEX IX_InsuranceConductMetrics_Regulator
        ON dbo.InsuranceConductMetrics (RegulatorCode, AsOfDate DESC);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.ConductRiskScores', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConductRiskScores (
        Id                      BIGINT              IDENTITY(1,1) PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
        InstitutionId           INT                 NOT NULL REFERENCES dbo.institutions(Id),
        RegulatorCode           VARCHAR(10)         NOT NULL,
        InstitutionType         VARCHAR(20)         NOT NULL,
        PeriodCode              VARCHAR(10)         NOT NULL,
        ScoreVersion            INT                 NOT NULL DEFAULT 1,
        MarketAbuseScore        DECIMAL(5,2)        NOT NULL DEFAULT 0,
        AMLEffectivenessScore   DECIMAL(5,2)        NOT NULL DEFAULT 0,
        InsuranceConductScore   DECIMAL(5,2)        NOT NULL DEFAULT 0,
        CustomerConductScore    DECIMAL(5,2)        NOT NULL DEFAULT 0,
        GovernanceScore         DECIMAL(5,2)        NOT NULL DEFAULT 0,
        SanctionHistoryScore    DECIMAL(5,2)        NOT NULL DEFAULT 0,
        CompositeScore          DECIMAL(5,2)        NOT NULL,
        RiskBand                VARCHAR(10)         NOT NULL,
        ActiveAlertCount        INT                 NOT NULL DEFAULT 0,
        ComputationRunId        UNIQUEIDENTIFIER    NOT NULL,
        ComputedAt              DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_ConductRiskScores_Institution
        ON dbo.ConductRiskScores (TenantId, InstitutionId, ComputedAt DESC);
    CREATE INDEX IX_ConductRiskScores_Band
        ON dbo.ConductRiskScores (RegulatorCode, RiskBand, ComputedAt DESC);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.WhistleblowerReports', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WhistleblowerReports (
        Id                          BIGINT              IDENTITY(1,1) PRIMARY KEY,
        TenantId                    UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
        CaseReference               VARCHAR(30)         NOT NULL,
        AnonymousToken              VARCHAR(64)         NOT NULL,
        RegulatorCode               VARCHAR(10)         NOT NULL,
        AllegedInstitutionId        INT                 NULL REFERENCES dbo.institutions(Id),
        AllegedInstitutionName      NVARCHAR(200)       NULL,
        Category                    VARCHAR(30)         NOT NULL,
        Summary                     NVARCHAR(2000)      NOT NULL,
        EvidenceDescription         NVARCHAR(1000)      NULL,
        EvidenceS3Keys              NVARCHAR(MAX)       NULL,
        Status                      VARCHAR(20)         NOT NULL DEFAULT 'RECEIVED',
        AssignedToUserId            INT                 NULL,
        PriorityScore               INT                 NOT NULL DEFAULT 50,
        ReceivedAt                  DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt                   DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_WhistleblowerReports_Ref UNIQUE (CaseReference),
        CONSTRAINT UQ_WhistleblowerReports_Token UNIQUE (AnonymousToken)
    );

    CREATE INDEX IX_WhistleblowerReports_Regulator
        ON dbo.WhistleblowerReports (TenantId, RegulatorCode, Status, ReceivedAt DESC);
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.WhistleblowerCaseEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WhistleblowerCaseEvents (
        Id                          BIGINT              IDENTITY(1,1) PRIMARY KEY,
        TenantId                    UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
        RegulatorCode               VARCHAR(10)         NOT NULL,
        WhistleblowerReportId       BIGINT              NOT NULL REFERENCES dbo.WhistleblowerReports(Id),
        EventType                   VARCHAR(30)         NOT NULL,
        Note                        NVARCHAR(1000)      NULL,
        PerformedByUserId           INT                 NULL,
        PerformedAt                 DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_WhistleblowerCaseEvents_Report
        ON dbo.WhistleblowerCaseEvents (WhistleblowerReportId, PerformedAt ASC);
END
""");

        migrationBuilder.Sql("""
IF NOT EXISTS (
    SELECT 1
    FROM dbo.SurveillanceRuleParameters
    WHERE RuleCode = 'BDC_RATE_MANIPULATION'
      AND RegulatorCode = 'CBN'
      AND InstitutionType = 'BDC'
      AND ParamName = 'ConsecutiveDaysOutsideBand'
      AND EffectiveFrom = CAST(GETUTCDATE() AS DATE)
)
BEGIN
    INSERT INTO dbo.SurveillanceRuleParameters
        (TenantId, RuleCode, RegulatorCode, InstitutionType, ParamName, ParamValue, Description)
    VALUES
        (NULL, 'BDC_RATE_MANIPULATION', 'CBN', 'BDC', 'ConsecutiveDaysOutsideBand', 5, 'Trigger if BDC sell rate remains outside the CBN permitted FX band for five or more consecutive business days.'),
        (NULL, 'BDC_RATE_MANIPULATION', 'CBN', 'BDC', 'BandTolerancePct', 1.500000, 'Additional tolerance above the official CBN interbank band, expressed in percentage points.'),
        (NULL, 'BDC_VOLUME_SPIKE', 'CBN', 'BDC', 'VolumeZScoreThreshold', 3.000000, 'Daily BDC volume anomaly threshold expressed as a rolling z-score.'),
        (NULL, 'BDC_VOLUME_SPIKE', 'CBN', 'BDC', 'LookbackDays', 30, 'Rolling lookback window for BDC volume baseline computation.'),
        (NULL, 'BDC_WASH_TRADE', 'CBN', 'BDC', 'CircularTxnWindowDays', 7, 'Maximum window for identifying circular BDC trades between counterparties.'),
        (NULL, 'BDC_WASH_TRADE', 'CBN', 'BDC', 'MinCircularAmountUSD', 50000, 'Minimum circular USD amount before a potential wash-trade pattern is raised.'),
        (NULL, 'CMO_UNUSUAL_TRADE', 'SEC', 'CMO', 'PreAnnouncementWindowDays', 3, 'Trading window before a corporate announcement that should be scrutinised for insider-style accumulation.'),
        (NULL, 'CMO_UNUSUAL_TRADE', 'SEC', 'CMO', 'VolumeMultiplierThreshold', 5.000000, 'Pre-announcement volume must exceed the historical baseline by this multiple to trigger.'),
        (NULL, 'CMO_LATE_REPORT', 'SEC', 'CMO', 'MaxReportingDelayHours', 24, 'Maximum SEC/NGX trade reporting delay before a late-reporting alert is raised.'),
        (NULL, 'CMO_CONCENTRATION', 'SEC', 'CMO', 'SingleSecurityConcentrationPct', 25.000000, 'Single-security trade concentration cap as a percentage of an operator''s trade book.'),
        (NULL, 'AML_LOW_STR', 'NFIU', 'ALL', 'STRZScoreThreshold', -2.000000, 'Institution is flagged when STR filing activity falls at least two standard deviations below peer behaviour.'),
        (NULL, 'AML_STRUCTURING', 'NFIU', 'ALL', 'CTRThresholdNGN', 5000000, 'Cash transaction reporting benchmark in NGN aligned to the prevailing NFIU cash threshold.'),
        (NULL, 'AML_STRUCTURING', 'NFIU', 'ALL', 'StructuringWindowDays', 3, 'Rolling window for sub-threshold structuring pattern assessment.'),
        (NULL, 'AML_TFS_FALSE_POS', 'NFIU', 'ALL', 'TFSFalsePositiveRateMax', 0.950000, 'False-positive rate above this ceiling indicates sanctions/TFS screening noise and weak tuning.'),
        (NULL, 'AML_TFS_FALSE_POS', 'NFIU', 'ALL', 'TFSFalsePositiveRateMin', 0.050000, 'False-positive rate below this floor indicates screening may not actually be taking place.'),
        (NULL, 'INS_CLAIMS_SUPPRESSION', 'NAICOM', 'INSURER', 'MinClaimsRatioPct', 30.000000, 'Claims ratio floor informed by NAICOM conduct expectations; persistent sub-30%% claims payout is suspicious.'),
        (NULL, 'INS_CLAIMS_SUPPRESSION', 'NAICOM', 'INSURER', 'PeerDeviation', 20.000000, 'Deviation from sector peer claims ratio, in percentage points, before suppression is flagged.'),
        (NULL, 'INS_PREMIUM_UNDER', 'NAICOM', 'INSURER', 'PremiumGapThresholdPct', 15.000000, 'Premium under-reporting gap as a percentage of expected premium before alerting.'),
        (NULL, 'INS_RELATED_REINS', 'NAICOM', 'INSURER', 'RelatedPartyReinsCapPct', 30.000000, 'Maximum related-party reinsurance share before a conduct concern is raised.');
END
""");

        RebuildTenantSecurityPolicyForRegulator(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.WhistleblowerCaseEvents', N'U') IS NOT NULL
    DROP TABLE dbo.WhistleblowerCaseEvents;

IF OBJECT_ID(N'dbo.WhistleblowerReports', N'U') IS NOT NULL
    DROP TABLE dbo.WhistleblowerReports;

IF OBJECT_ID(N'dbo.ConductRiskScores', N'U') IS NOT NULL
    DROP TABLE dbo.ConductRiskScores;

IF OBJECT_ID(N'dbo.InsuranceConductMetrics', N'U') IS NOT NULL
    DROP TABLE dbo.InsuranceConductMetrics;

IF OBJECT_ID(N'dbo.AMLConductMetrics', N'U') IS NOT NULL
    DROP TABLE dbo.AMLConductMetrics;

IF OBJECT_ID(N'dbo.CMOTradeReports', N'U') IS NOT NULL
    DROP TABLE dbo.CMOTradeReports;

IF OBJECT_ID(N'dbo.CorporateAnnouncements', N'U') IS NOT NULL
    DROP TABLE dbo.CorporateAnnouncements;

IF OBJECT_ID(N'dbo.BDCFXTransactions', N'U') IS NOT NULL
    DROP TABLE dbo.BDCFXTransactions;

IF OBJECT_ID(N'dbo.SurveillanceAlertResolutions', N'U') IS NOT NULL
    DROP TABLE dbo.SurveillanceAlertResolutions;

IF OBJECT_ID(N'dbo.SurveillanceAlerts', N'U') IS NOT NULL
    DROP TABLE dbo.SurveillanceAlerts;

IF OBJECT_ID(N'dbo.SurveillanceRuleParameters', N'U') IS NOT NULL
    DROP TABLE dbo.SurveillanceRuleParameters;
""");

        RebuildTenantSecurityPolicyForRegulator(migrationBuilder);
    }

    private static void RebuildTenantSecurityPolicyForRegulator(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.TenantSecurityPolicy', N'SP') IS NOT NULL
    DROP SECURITY POLICY dbo.TenantSecurityPolicy;

IF OBJECT_ID(N'dbo.fn_TenantFilter', N'IF') IS NOT NULL
    DROP FUNCTION dbo.fn_TenantFilter;

EXEC('
    CREATE FUNCTION dbo.fn_TenantFilter(@TenantId UNIQUEIDENTIFIER)
    RETURNS TABLE
    WITH SCHEMABINDING
    AS
    RETURN
    SELECT 1 AS fn_accessResult
    WHERE @TenantId = CAST(SESSION_CONTEXT(N''TenantId'') AS UNIQUEIDENTIFIER)
       OR @TenantId IS NULL
       OR SESSION_CONTEXT(N''TenantId'') IS NULL
       OR EXISTS (
            SELECT 1
            FROM dbo.tenants p
            WHERE p.TenantId = CAST(SESSION_CONTEXT(N''TenantId'') AS UNIQUEIDENTIFIER)
              AND p.TenantType = N''WhiteLabelPartner''
              AND EXISTS (
                    SELECT 1
                    FROM dbo.tenants c
                    WHERE c.TenantId = @TenantId
                      AND c.ParentTenantId = p.TenantId
              )
       )
       OR (
            CAST(SESSION_CONTEXT(N''TenantType'') AS NVARCHAR(30)) = N''Regulator''
            AND CAST(SESSION_CONTEXT(N''RegulatorCode'') AS NVARCHAR(30)) IS NOT NULL
            AND EXISTS (
                SELECT 1
                FROM dbo.tenant_licence_types tlt
                INNER JOIN dbo.licence_module_matrix lmm ON lmm.LicenceTypeId = tlt.LicenceTypeId
                INNER JOIN dbo.modules m ON m.Id = lmm.ModuleId
                WHERE tlt.TenantId = @TenantId
                  AND tlt.IsActive = 1
                  AND (lmm.IsRequired = 1 OR lmm.IsOptional = 1)
                  AND m.RegulatorCode = CAST(SESSION_CONTEXT(N''RegulatorCode'') AS NVARCHAR(30))
            )
       );');

DECLARE @sql NVARCHAR(MAX) = N'CREATE SECURITY POLICY dbo.TenantSecurityPolicy' + CHAR(13);
DECLARE @first BIT = 1;

DECLARE tenant_cursor CURSOR FAST_FORWARD FOR
SELECT s.name AS SchemaName, t.name AS TableName
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE c.name = 'TenantId'
  AND t.is_ms_shipped = 0;

DECLARE @schemaName SYSNAME, @tableName SYSNAME;
OPEN tenant_cursor;
FETCH NEXT FROM tenant_cursor INTO @schemaName, @tableName;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF @first = 0 SET @sql += N',' + CHAR(13);
    SET @sql += N'    ADD FILTER PREDICATE dbo.fn_TenantFilter(TenantId) ON [' + @schemaName + N'].[' + @tableName + N']';
    SET @first = 0;
    FETCH NEXT FROM tenant_cursor INTO @schemaName, @tableName;
END;
CLOSE tenant_cursor;
DEALLOCATE tenant_cursor;

DECLARE tenant_cursor_block CURSOR FAST_FORWARD FOR
SELECT s.name AS SchemaName, t.name AS TableName
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE c.name = 'TenantId'
  AND t.is_ms_shipped = 0;

OPEN tenant_cursor_block;
FETCH NEXT FROM tenant_cursor_block INTO @schemaName, @tableName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql += N',' + CHAR(13)
             + N'    ADD BLOCK PREDICATE dbo.fn_TenantFilter(TenantId) ON [' + @schemaName + N'].[' + @tableName + N']';
    FETCH NEXT FROM tenant_cursor_block INTO @schemaName, @tableName;
END;
CLOSE tenant_cursor_block;
DEALLOCATE tenant_cursor_block;

SET @sql += CHAR(13) + N'WITH (STATE = ON);';
EXEC sp_executesql @sql;
""");
    }
}
