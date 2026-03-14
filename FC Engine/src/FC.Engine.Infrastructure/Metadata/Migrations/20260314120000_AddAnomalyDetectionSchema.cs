#nullable enable
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FC.Engine.Infrastructure.Metadata.Migrations;

/// <summary>
/// AI-01: Anomaly Detection — creates the nine anomaly tables in the meta schema
/// for AI-powered statistical anomaly detection on submitted returns.
/// </summary>
[DbContext(typeof(MetadataDbContext))]
[Migration("20260314120000_AddAnomalyDetectionSchema")]
public partial class AddAnomalyDetectionSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ═══════════════════════════════════════════════════════════
        // Ensure the meta schema exists
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF SCHEMA_ID('meta') IS NULL
                EXEC('CREATE SCHEMA meta');
        ");

        // ═══════════════════════════════════════════════════════════
        // 1. anomaly_threshold_config
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.anomaly_threshold_config', N'U') IS NULL
            BEGIN
                CREATE TABLE meta.anomaly_threshold_config (
                    Id              INT             IDENTITY(1,1) PRIMARY KEY,
                    ConfigKey       VARCHAR(100)    NOT NULL,
                    ConfigValue     DECIMAL(18,6)   NOT NULL,
                    Description     NVARCHAR(1000)  NOT NULL,
                    EffectiveFrom   DATETIME2(3)    NULL,
                    EffectiveTo     DATETIME2(3)    NULL,
                    CreatedBy       VARCHAR(100)    NOT NULL,
                    CreatedAt       DATETIME2(3)    NOT NULL DEFAULT SYSUTCDATETIME(),
                    UpdatedAt       DATETIME2(3)    NOT NULL DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE NONCLUSTERED INDEX IX_anomaly_threshold_config_key_from
                    ON meta.anomaly_threshold_config (ConfigKey, EffectiveFrom);

                CREATE NONCLUSTERED INDEX IX_anomaly_threshold_config_key_active
                    ON meta.anomaly_threshold_config (ConfigKey)
                    WHERE [EffectiveTo] IS NULL;

                -- Seed thresholds
                SET IDENTITY_INSERT meta.anomaly_threshold_config ON;
                INSERT INTO meta.anomaly_threshold_config (Id, ConfigKey, ConfigValue, [Description], EffectiveFrom, CreatedBy, CreatedAt, UpdatedAt) VALUES
                    (1,  'zscore.alert_threshold',       3.0,    'Absolute z-score threshold for alert findings.',                            '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (2,  'zscore.warning_threshold',     2.0,    'Absolute z-score threshold for warning findings.',                          '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (3,  'zscore.info_threshold',        1.5,    'Absolute z-score threshold for informational findings.',                    '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (4,  'correlation.deviation_threshold', 0.30, 'Allowed percentage deviation from expected correlated value.',              '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (5,  'correlation.min_r_squared',    0.60,   'Minimum R-squared for learned correlation rules.',                          '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (6,  'temporal.jump_pct_alert',      50.0,   'Period-over-period percentage jump that maps to alert severity.',            '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (7,  'temporal.jump_pct_warning',    30.0,   'Period-over-period percentage jump that maps to warning severity.',           '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (8,  'temporal.min_periods',         3.0,    'Minimum historical periods required before temporal analysis activates.',    '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (9,  'peer.iqr_multiplier',          2.5,    'IQR multiplier used to compute peer outlier fences.',                       '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (10, 'peer.min_peers',               5.0,    'Minimum peer submissions required before peer comparison activates.',       '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (11, 'coldstart.min_observations',   30.0,   'Minimum observations before a field uses learned stats instead of rules.',  '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (12, 'quality.anomaly_weight_alert',  10.0,  'Penalty points for each unacknowledged alert finding.',                     '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (13, 'quality.anomaly_weight_warning', 5.0,  'Penalty points for each unacknowledged warning finding.',                   '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (14, 'quality.anomaly_weight_info',   2.0,   'Penalty points for each unacknowledged informational finding.',              '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (15, 'quality.max_penalty',          100.0,  'Maximum capped quality penalty.',                                           '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12'),
                    (16, 'model.retraining_day_of_month', 1.0,   'Configured day of month for scheduled anomaly model retraining.',           '2026-03-12', 'SYSTEM', '2026-03-12', '2026-03-12');
                SET IDENTITY_INSERT meta.anomaly_threshold_config OFF;
            END
        ");

        // ═══════════════════════════════════════════════════════════
        // 2. anomaly_model_versions
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.anomaly_model_versions', N'U') IS NULL
            BEGIN
                CREATE TABLE meta.anomaly_model_versions (
                    Id                    INT             IDENTITY(1,1) PRIMARY KEY,
                    ModuleCode            VARCHAR(40)     NOT NULL,
                    RegulatorCode         VARCHAR(10)     NOT NULL,
                    VersionNumber         INT             NOT NULL,
                    [Status]              VARCHAR(20)     NOT NULL DEFAULT 'SHADOW',
                    TrainingStartedAt     DATETIME2(3)    NOT NULL DEFAULT SYSUTCDATETIME(),
                    TrainingCompletedAt   DATETIME2(3)    NULL,
                    SubmissionCount       INT             NOT NULL DEFAULT 0,
                    ObservationCount      INT             NOT NULL DEFAULT 0,
                    TenantCount           INT             NOT NULL DEFAULT 0,
                    PeriodCount           INT             NOT NULL DEFAULT 0,
                    PromotedAt            DATETIME2(3)    NULL,
                    PromotedBy            VARCHAR(100)    NULL,
                    RetiredAt             DATETIME2(3)    NULL,
                    Notes                 NVARCHAR(2000)  NULL,
                    CreatedAt             DATETIME2(3)    NOT NULL DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE NONCLUSTERED INDEX IX_anomaly_model_versions_module_version
                    ON meta.anomaly_model_versions (ModuleCode, VersionNumber);

                CREATE NONCLUSTERED INDEX IX_anomaly_model_versions_module_status
                    ON meta.anomaly_model_versions (ModuleCode, [Status]);
            END
        ");

        // ═══════════════════════════════════════════════════════════
        // 3. anomaly_field_models
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.anomaly_field_models', N'U') IS NULL
            BEGIN
                CREATE TABLE meta.anomaly_field_models (
                    Id                INT             IDENTITY(1,1) PRIMARY KEY,
                    ModelVersionId    INT             NOT NULL REFERENCES meta.anomaly_model_versions(Id) ON DELETE CASCADE,
                    ModuleCode        VARCHAR(40)     NOT NULL,
                    FieldCode         VARCHAR(120)    NOT NULL,
                    FieldLabel        NVARCHAR(200)   NOT NULL,
                    DistributionType  VARCHAR(20)     NOT NULL DEFAULT 'NORMAL',
                    Observations      INT             NOT NULL DEFAULT 0,
                    MeanValue         DECIMAL(28,8)   NULL,
                    StdDev            DECIMAL(28,8)   NULL,
                    MedianValue       DECIMAL(28,8)   NULL,
                    Q1Value           DECIMAL(28,8)   NULL,
                    Q3Value           DECIMAL(28,8)   NULL,
                    MinObserved       DECIMAL(28,8)   NULL,
                    MaxObserved       DECIMAL(28,8)   NULL,
                    Percentile05      DECIMAL(28,8)   NULL,
                    Percentile95      DECIMAL(28,8)   NULL,
                    IsColdStart       BIT             NOT NULL DEFAULT 0,
                    RuleBasedMin      DECIMAL(28,8)   NULL,
                    RuleBasedMax      DECIMAL(28,8)   NULL,
                    CreatedAt         DATETIME2(3)    NOT NULL DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE NONCLUSTERED INDEX IX_anomaly_field_models_version_field
                    ON meta.anomaly_field_models (ModelVersionId, FieldCode);

                CREATE NONCLUSTERED INDEX IX_anomaly_field_models_lookup
                    ON meta.anomaly_field_models (ModuleCode, FieldCode);
            END
        ");

        // ═══════════════════════════════════════════════════════════
        // 4. anomaly_correlation_rules
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.anomaly_correlation_rules', N'U') IS NULL
            BEGIN
                CREATE TABLE meta.anomaly_correlation_rules (
                    Id                       INT             IDENTITY(1,1) PRIMARY KEY,
                    ModelVersionId           INT             NOT NULL REFERENCES meta.anomaly_model_versions(Id) ON DELETE CASCADE,
                    ModuleCode               VARCHAR(40)     NOT NULL,
                    FieldCodeA               VARCHAR(120)    NOT NULL,
                    FieldLabelA              NVARCHAR(200)   NOT NULL,
                    FieldCodeB               VARCHAR(120)    NOT NULL,
                    FieldLabelB              NVARCHAR(200)   NOT NULL,
                    CorrelationCoefficient   DECIMAL(10,6)   NULL,
                    RSquared                 DECIMAL(10,6)   NULL,
                    Slope                    DECIMAL(18,8)   NULL,
                    Intercept                DECIMAL(18,8)   NULL,
                    Observations             INT             NOT NULL DEFAULT 0,
                    IsActive                 BIT             NOT NULL DEFAULT 1,
                    CreatedAt                DATETIME2(3)    NOT NULL DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE NONCLUSTERED INDEX IX_anomaly_correlation_rules_version_fields
                    ON meta.anomaly_correlation_rules (ModelVersionId, FieldCodeA, FieldCodeB);

                CREATE NONCLUSTERED INDEX IX_anomaly_correlation_rules_module_active
                    ON meta.anomaly_correlation_rules (ModuleCode, IsActive);
            END
        ");

        // ═══════════════════════════════════════════════════════════
        // 5. anomaly_peer_group_stats
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.anomaly_peer_group_stats', N'U') IS NULL
            BEGIN
                CREATE TABLE meta.anomaly_peer_group_stats (
                    Id                   INT             IDENTITY(1,1) PRIMARY KEY,
                    ModelVersionId       INT             NOT NULL REFERENCES meta.anomaly_model_versions(Id) ON DELETE CASCADE,
                    ModuleCode           VARCHAR(40)     NOT NULL,
                    FieldCode            VARCHAR(120)    NOT NULL,
                    LicenceCategory      VARCHAR(60)     NOT NULL,
                    InstitutionSizeBand  VARCHAR(20)     NOT NULL DEFAULT 'ALL',
                    PeerCount            INT             NOT NULL DEFAULT 0,
                    PeerMean             DECIMAL(28,8)   NULL,
                    PeerMedian           DECIMAL(28,8)   NULL,
                    PeerStdDev           DECIMAL(28,8)   NULL,
                    PeerQ1               DECIMAL(28,8)   NULL,
                    PeerQ3               DECIMAL(28,8)   NULL,
                    PeerMin              DECIMAL(28,8)   NULL,
                    PeerMax              DECIMAL(28,8)   NULL,
                    PeriodCode           VARCHAR(20)     NOT NULL,
                    CreatedAt            DATETIME2(3)    NOT NULL DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE NONCLUSTERED INDEX IX_anomaly_peer_group_stats_version_field_cat_period
                    ON meta.anomaly_peer_group_stats (ModelVersionId, FieldCode, LicenceCategory, PeriodCode);

                CREATE NONCLUSTERED INDEX IX_anomaly_peer_group_stats_lookup
                    ON meta.anomaly_peer_group_stats (ModuleCode, LicenceCategory, PeriodCode);
            END
        ");

        // ═══════════════════════════════════════════════════════════
        // 6. anomaly_rule_baselines
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.anomaly_rule_baselines', N'U') IS NULL
            BEGIN
                CREATE TABLE meta.anomaly_rule_baselines (
                    Id              INT             IDENTITY(1,1) PRIMARY KEY,
                    RegulatorCode   VARCHAR(10)     NOT NULL,
                    ModuleCode      VARCHAR(40)     NULL,
                    FieldCode       VARCHAR(120)    NOT NULL,
                    FieldLabel      NVARCHAR(200)   NOT NULL,
                    MinimumValue    DECIMAL(28,8)   NULL,
                    MaximumValue    DECIMAL(28,8)   NULL,
                    Notes           NVARCHAR(1000)  NOT NULL,
                    IsActive        BIT             NOT NULL DEFAULT 1,
                    CreatedAt       DATETIME2(3)    NOT NULL DEFAULT SYSUTCDATETIME()
                );

                CREATE NONCLUSTERED INDEX IX_anomaly_rule_baselines_lookup
                    ON meta.anomaly_rule_baselines (RegulatorCode, ModuleCode, FieldCode);

                -- Seed baselines
                SET IDENTITY_INSERT meta.anomaly_rule_baselines ON;
                INSERT INTO meta.anomaly_rule_baselines (Id, RegulatorCode, FieldCode, FieldLabel, MinimumValue, MaximumValue, Notes, IsActive, CreatedAt) VALUES
                    (1,  'CBN',    'carratio',        'Capital Adequacy Ratio',          10,                  50,                  'CBN prudential baseline for capital adequacy.',       1, '2026-03-12'),
                    (2,  'CBN',    'nplratio',         'Non-Performing Loan Ratio',       0,                   35,                  'CBN prudential baseline for NPL ratio.',              1, '2026-03-12'),
                    (3,  'CBN',    'liquidityratio',   'Liquidity Ratio',                 20,                  80,                  'CBN prudential baseline for liquidity ratio.',        1, '2026-03-12'),
                    (4,  'CBN',    'loandepositratio', 'Loan to Deposit Ratio',           30,                  85,                  'CBN prudential baseline for loan to deposit ratio.',  1, '2026-03-12'),
                    (5,  'CBN',    'costincomeratio',  'Cost to Income Ratio',            30,                  90,                  'CBN prudential baseline for cost to income ratio.',   1, '2026-03-12'),
                    (6,  'CBN',    'roa',              'Return on Assets',                -5,                  10,                  'CBN prudential baseline for return on assets.',       1, '2026-03-12'),
                    (7,  'CBN',    'roe',              'Return on Equity',                -20,                 40,                  'CBN prudential baseline for return on equity.',       1, '2026-03-12'),
                    (8,  'NDIC',   'insureddeposits',  'Insured Deposits',                0,                   5000000000000,       'NDIC status return cold-start range.',                1, '2026-03-12'),
                    (9,  'NDIC',   'depositpremiumdue','Deposit Insurance Premium Due',   0,                   50000000000,         'NDIC premium baseline.',                              1, '2026-03-12'),
                    (10, 'NAICOM', 'grosspremium',     'Gross Premium Written',           0,                   500000000000,        'NAICOM quarterly return baseline.',                   1, '2026-03-12'),
                    (11, 'NAICOM', 'combinedratio',    'Combined Ratio',                  30,                  200,                 'NAICOM combined ratio baseline.',                     1, '2026-03-12'),
                    (12, 'NAICOM', 'solvencymargin',   'Solvency Margin',                 100,                 500,                 'NAICOM solvency margin baseline.',                    1, '2026-03-12'),
                    (13, 'SEC',    'netcapital',       'Net Capital',                     0,                   500000000000,        'SEC capital market operator baseline.',                1, '2026-03-12'),
                    (14, 'SEC',    'liquidcapital',    'Liquid Capital',                  0,                   500000000000,        'SEC liquid capital baseline.',                         1, '2026-03-12'),
                    (15, 'SEC',    'clientassetsaum',  'Client Assets Under Management',  0,                   10000000000000,      'SEC AUM baseline.',                                   1, '2026-03-12'),
                    (16, 'SEC',    'capitaladequacy',  'Capital Adequacy',                10,                  100,                 'SEC capital adequacy ratio baseline.',                 1, '2026-03-12');
                SET IDENTITY_INSERT meta.anomaly_rule_baselines OFF;
            END
        ");

        // ═══════════════════════════════════════════════════════════
        // 7. anomaly_seed_correlation_rules
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.anomaly_seed_correlation_rules', N'U') IS NULL
            BEGIN
                CREATE TABLE meta.anomaly_seed_correlation_rules (
                    Id                       INT             IDENTITY(1,1) PRIMARY KEY,
                    RegulatorCode            VARCHAR(10)     NOT NULL,
                    ModuleCode               VARCHAR(40)     NULL,
                    FieldCodeA               VARCHAR(120)    NOT NULL,
                    FieldLabelA              NVARCHAR(200)   NOT NULL,
                    FieldCodeB               VARCHAR(120)    NOT NULL,
                    FieldLabelB              NVARCHAR(200)   NOT NULL,
                    CorrelationCoefficient   DECIMAL(10,6)   NULL,
                    RSquared                 DECIMAL(10,6)   NULL,
                    Slope                    DECIMAL(18,8)   NULL,
                    Intercept                DECIMAL(18,8)   NULL,
                    Notes                    NVARCHAR(1000)  NOT NULL,
                    IsActive                 BIT             NOT NULL DEFAULT 1,
                    CreatedAt                DATETIME2(3)    NOT NULL DEFAULT SYSUTCDATETIME()
                );

                CREATE NONCLUSTERED INDEX IX_anomaly_seed_correlation_rules_lookup
                    ON meta.anomaly_seed_correlation_rules (RegulatorCode, ModuleCode, FieldCodeA, FieldCodeB);

                -- Seed correlation rules (CBN)
                SET IDENTITY_INSERT meta.anomaly_seed_correlation_rules ON;
                INSERT INTO meta.anomaly_seed_correlation_rules (Id, RegulatorCode, FieldCodeA, FieldLabelA, FieldCodeB, FieldLabelB, CorrelationCoefficient, RSquared, Slope, Intercept, Notes, IsActive, CreatedAt) VALUES
                    (1, 'CBN', 'totalassets',       'Total Assets',         'totalliabilities',  'Total Liabilities',      0.980000, 0.960000, 0.85000000, 1000000.00000000, 'Total liabilities should track total assets closely.',                1, '2026-03-12'),
                    (2, 'CBN', 'totalassets',       'Total Assets',         'shareholdersfunds', 'Shareholders Funds',     0.920000, 0.850000, 0.15000000, 500000.00000000,  'Shareholders funds should scale with total assets.',                  1, '2026-03-12'),
                    (3, 'CBN', 'totalloans',        'Total Loans',          'nplamount',         'NPL Amount',             0.750000, 0.560000, 0.05000000, 100000.00000000,  'NPL amount tends to rise with loan book size.',                       1, '2026-03-12'),
                    (4, 'CBN', 'totaldeposits',     'Total Deposits',       'totalloans',        'Total Loans',            0.880000, 0.770000, 0.65000000, 2000000.00000000, 'Loan book should remain directionally aligned with deposits.',        1, '2026-03-12'),
                    (5, 'CBN', 'riskweightedassets', 'Risk Weighted Assets', 'carratio',          'Capital Adequacy Ratio', -0.350000, 0.120000, -0.00010000, 20.00000000,    'CAR usually compresses as RWA rises.',                                1, '2026-03-12'),
                    (6, 'CBN', 'interestincome',    'Interest Income',      'interestexpense',   'Interest Expense',       0.900000, 0.810000, 0.45000000, 50000.00000000,   'Interest income and expense should show a stable relationship.',      1, '2026-03-12'),
                    (7, 'CBN', 'totalloans',        'Total Loans',          'provisionamount',   'Provision Amount',       0.800000, 0.640000, 0.03000000, 50000.00000000,   'Provisioning should broadly move with loans.',                        1, '2026-03-12'),
                    (8, 'CBN', 'totaldeposits',     'Total Deposits',       'liquidassets',      'Liquid Assets',          0.850000, 0.720000, 0.30000000, 1000000.00000000, 'Liquidity buffers should scale with deposits.',                       1, '2026-03-12');
                SET IDENTITY_INSERT meta.anomaly_seed_correlation_rules OFF;
            END
        ");

        // ═══════════════════════════════════════════════════════════
        // 8. anomaly_reports (has TenantId — needs RLS)
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.anomaly_reports', N'U') IS NULL
            BEGIN
                CREATE TABLE meta.anomaly_reports (
                    Id                    INT                 IDENTITY(1,1) PRIMARY KEY,
                    TenantId              UNIQUEIDENTIFIER    NOT NULL,
                    InstitutionId         INT                 NOT NULL,
                    InstitutionName       NVARCHAR(200)       NOT NULL,
                    SubmissionId          INT                 NOT NULL,
                    ModuleCode            VARCHAR(40)         NOT NULL,
                    RegulatorCode         VARCHAR(10)         NOT NULL,
                    PeriodCode            VARCHAR(20)         NOT NULL,
                    ModelVersionId        INT                 NOT NULL REFERENCES meta.anomaly_model_versions(Id) ON DELETE NO ACTION,
                    OverallQualityScore   DECIMAL(6,2)        NOT NULL DEFAULT 100,
                    TotalFieldsAnalysed   INT                 NOT NULL DEFAULT 0,
                    TotalFindings         INT                 NOT NULL DEFAULT 0,
                    AlertCount            INT                 NOT NULL DEFAULT 0,
                    WarningCount          INT                 NOT NULL DEFAULT 0,
                    InfoCount             INT                 NOT NULL DEFAULT 0,
                    RelationshipFindings  INT                 NOT NULL DEFAULT 0,
                    TemporalFindings      INT                 NOT NULL DEFAULT 0,
                    PeerFindings          INT                 NOT NULL DEFAULT 0,
                    TrafficLight          VARCHAR(10)         NOT NULL DEFAULT 'GREEN',
                    NarrativeSummary      NVARCHAR(2000)      NOT NULL,
                    AnalysedAt            DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME(),
                    AnalysisDurationMs    INT                 NULL,
                    CreatedAt             DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE NONCLUSTERED INDEX IX_anomaly_reports_submission_model
                    ON meta.anomaly_reports (SubmissionId, ModelVersionId);

                CREATE NONCLUSTERED INDEX IX_anomaly_reports_tenant_period
                    ON meta.anomaly_reports (TenantId, PeriodCode);

                CREATE NONCLUSTERED INDEX IX_anomaly_reports_module_period
                    ON meta.anomaly_reports (ModuleCode, PeriodCode);
            END
        ");

        // ═══════════════════════════════════════════════════════════
        // 9. anomaly_findings (has TenantId — needs RLS)
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.anomaly_findings', N'U') IS NULL
            BEGIN
                CREATE TABLE meta.anomaly_findings (
                    Id                       INT                 IDENTITY(1,1) PRIMARY KEY,
                    TenantId                 UNIQUEIDENTIFIER    NOT NULL,
                    SubmissionId             INT                 NOT NULL,
                    AnomalyReportId          INT                 NOT NULL REFERENCES meta.anomaly_reports(Id) ON DELETE CASCADE,
                    FindingType              VARCHAR(20)         NOT NULL DEFAULT 'FIELD',
                    Severity                 VARCHAR(10)         NOT NULL DEFAULT 'INFO',
                    DetectionMethod          VARCHAR(30)         NOT NULL,
                    FieldCode                VARCHAR(120)        NOT NULL,
                    FieldLabel               NVARCHAR(200)       NOT NULL,
                    RelatedFieldCode         VARCHAR(120)        NULL,
                    RelatedFieldLabel        NVARCHAR(200)       NULL,
                    ReportedValue            DECIMAL(28,8)       NULL,
                    RelatedValue             DECIMAL(28,8)       NULL,
                    ExpectedValue            DECIMAL(28,8)       NULL,
                    ExpectedRangeLow         DECIMAL(28,8)       NULL,
                    ExpectedRangeHigh        DECIMAL(28,8)       NULL,
                    HistoricalMean           DECIMAL(28,8)       NULL,
                    HistoricalStdDev         DECIMAL(28,8)       NULL,
                    BaselineValue            DECIMAL(28,8)       NULL,
                    DeviationPercent         DECIMAL(10,4)       NULL,
                    ZScore                   FLOAT               NULL,
                    PeerCount                INT                 NULL,
                    PeerGroup                NVARCHAR(100)       NULL,
                    Explanation              NVARCHAR(4000)      NOT NULL,
                    IsAcknowledged           BIT                 NOT NULL DEFAULT 0,
                    AcknowledgedBy           VARCHAR(100)        NULL,
                    AcknowledgedAt           DATETIME2(3)        NULL,
                    AcknowledgementReason    NVARCHAR(1000)      NULL,
                    CreatedAt                DATETIME2(3)        NOT NULL DEFAULT SYSUTCDATETIME()
                );

                CREATE NONCLUSTERED INDEX IX_anomaly_findings_report
                    ON meta.anomaly_findings (AnomalyReportId);

                CREATE NONCLUSTERED INDEX IX_anomaly_findings_tenant_ack
                    ON meta.anomaly_findings (TenantId, IsAcknowledged, Severity);

                CREATE NONCLUSTERED INDEX IX_anomaly_findings_submission_field
                    ON meta.anomaly_findings (SubmissionId, FieldCode);
            END
        ");

        // ═══════════════════════════════════════════════════════════
        // RLS: Add anomaly_reports and anomaly_findings to policy
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            BEGIN TRY
                IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = 'TenantSecurityPolicy')
                BEGIN
                    DROP SECURITY POLICY dbo.TenantSecurityPolicy;
                END

                DECLARE @policySql NVARCHAR(MAX) = 'CREATE SECURITY POLICY dbo.TenantSecurityPolicy' + CHAR(13);
                DECLARE @first BIT = 1;

                SELECT
                    @policySql = @policySql +
                        CASE WHEN @first = 1 THEN '    ' ELSE '   ,' END +
                        'ADD FILTER PREDICATE dbo.fn_TenantFilter(TenantId) ON ' +
                        QUOTENAME(SCHEMA_NAME(t.schema_id)) + '.' + QUOTENAME(t.name) + CHAR(13),
                    @first = 0
                FROM sys.tables t
                INNER JOIN sys.columns c ON t.object_id = c.object_id
                WHERE c.name = 'TenantId'
                  AND t.name <> 'tenants'
                ORDER BY t.name;

                SELECT
                    @policySql = @policySql +
                        '   ,ADD BLOCK PREDICATE dbo.fn_TenantFilter(TenantId) ON ' +
                        QUOTENAME(SCHEMA_NAME(t.schema_id)) + '.' + QUOTENAME(t.name) + CHAR(13)
                FROM sys.tables t
                INNER JOIN sys.columns c ON t.object_id = c.object_id
                WHERE c.name = 'TenantId'
                  AND t.name <> 'tenants'
                ORDER BY t.name;

                SET @policySql = @policySql + 'WITH (STATE = ON);';

                EXEC sp_executesql @policySql;
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (3728, 33268, 33280)
                    THROW;
            END CATCH
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // ═══════════════════════════════════════════════════════════
        // Drop RLS predicates first
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            BEGIN TRY
                IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = 'TenantSecurityPolicy')
                BEGIN
                    DROP SECURITY POLICY dbo.TenantSecurityPolicy;
                END
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (3728, 33268, 33280)
                    THROW;
            END CATCH
        ");

        // ═══════════════════════════════════════════════════════════
        // Drop tables in reverse dependency order
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.anomaly_findings', N'U') IS NOT NULL
                DROP TABLE meta.anomaly_findings;

            IF OBJECT_ID(N'meta.anomaly_reports', N'U') IS NOT NULL
                DROP TABLE meta.anomaly_reports;

            IF OBJECT_ID(N'meta.anomaly_seed_correlation_rules', N'U') IS NOT NULL
                DROP TABLE meta.anomaly_seed_correlation_rules;

            IF OBJECT_ID(N'meta.anomaly_rule_baselines', N'U') IS NOT NULL
                DROP TABLE meta.anomaly_rule_baselines;

            IF OBJECT_ID(N'meta.anomaly_peer_group_stats', N'U') IS NOT NULL
                DROP TABLE meta.anomaly_peer_group_stats;

            IF OBJECT_ID(N'meta.anomaly_correlation_rules', N'U') IS NOT NULL
                DROP TABLE meta.anomaly_correlation_rules;

            IF OBJECT_ID(N'meta.anomaly_field_models', N'U') IS NOT NULL
                DROP TABLE meta.anomaly_field_models;

            IF OBJECT_ID(N'meta.anomaly_model_versions', N'U') IS NOT NULL
                DROP TABLE meta.anomaly_model_versions;

            IF OBJECT_ID(N'meta.anomaly_threshold_config', N'U') IS NOT NULL
                DROP TABLE meta.anomaly_threshold_config;
        ");

        // ═══════════════════════════════════════════════════════════
        // Rebuild RLS without the dropped tables
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            BEGIN TRY
                DECLARE @policySql NVARCHAR(MAX) = 'CREATE SECURITY POLICY dbo.TenantSecurityPolicy' + CHAR(13);
                DECLARE @first BIT = 1;

                SELECT
                    @policySql = @policySql +
                        CASE WHEN @first = 1 THEN '    ' ELSE '   ,' END +
                        'ADD FILTER PREDICATE dbo.fn_TenantFilter(TenantId) ON ' +
                        QUOTENAME(SCHEMA_NAME(t.schema_id)) + '.' + QUOTENAME(t.name) + CHAR(13),
                    @first = 0
                FROM sys.tables t
                INNER JOIN sys.columns c ON t.object_id = c.object_id
                WHERE c.name = 'TenantId'
                  AND t.name <> 'tenants'
                ORDER BY t.name;

                SELECT
                    @policySql = @policySql +
                        '   ,ADD BLOCK PREDICATE dbo.fn_TenantFilter(TenantId) ON ' +
                        QUOTENAME(SCHEMA_NAME(t.schema_id)) + '.' + QUOTENAME(t.name) + CHAR(13)
                FROM sys.tables t
                INNER JOIN sys.columns c ON t.object_id = c.object_id
                WHERE c.name = 'TenantId'
                  AND t.name <> 'tenants'
                ORDER BY t.name;

                SET @policySql = @policySql + 'WITH (STATE = ON);';

                EXEC sp_executesql @policySql;
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (3728, 33268, 33280)
                    THROW;
            END CATCH
        ");
    }
}
