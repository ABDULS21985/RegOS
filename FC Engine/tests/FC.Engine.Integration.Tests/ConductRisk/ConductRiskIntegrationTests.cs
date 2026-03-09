using System.Data;
using Dapper;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Models;
using FC.Engine.Infrastructure;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Xunit;

namespace FC.Engine.Integration.Tests.ConductRisk;

[CollectionDefinition("ConductRiskIntegration")]
public sealed class ConductRiskIntegrationCollection : ICollectionFixture<ConductRiskFixture>;

[Collection("ConductRiskIntegration")]
public sealed class ConductRiskIntegrationTests : IClassFixture<ConductRiskFixture>
{
    private readonly ConductRiskFixture _fx;

    public ConductRiskIntegrationTests(ConductRiskFixture fx) => _fx = fx;

    [Fact]
    public async Task Bdc_RateManipulation_FiveConsecutiveBreaches_RaisesHighAlert()
    {
        var runId = Guid.NewGuid();
        await _fx.SeedBdcRatesAsync(101, 6, 1550m, 1565m, 1535m, 1585m, "2026-Q1");

        var count = await _fx.Bdc.DetectRateManipulationAsync("CBN", "2026-Q1", runId);

        Assert.True(count >= 1);

        using var conn = await _fx.Db.CreateConnectionAsync(null);
        var alert = await conn.QuerySingleOrDefaultAsync<AlertRow>(
            """
            SELECT AlertCode, Severity
            FROM dbo.SurveillanceAlerts
            WHERE AlertCode = 'BDC_RATE_MANIPULATION'
              AND DetectionRunId = @RunId
            """,
            new { RunId = runId });

        Assert.NotNull(alert);
        Assert.Contains(alert!.Severity, new[] { "HIGH", "CRITICAL" });
    }

    [Fact]
    public async Task Bdc_RateManipulation_WithinBand_RaisesNoAlert()
    {
        var runId = Guid.NewGuid();
        await _fx.SeedBdcRatesAsync(102, 0, 1550m, 1565m, 1535m, 1552m, "2026-Q1");

        var count = await _fx.Bdc.DetectRateManipulationAsync("CBN", "2026-Q1", runId);

        using var conn = await _fx.Db.CreateConnectionAsync(null);
        var alertCount = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM dbo.SurveillanceAlerts
            WHERE InstitutionId = 102
              AND AlertCode = 'BDC_RATE_MANIPULATION'
              AND DetectionRunId = @RunId
            """,
            new { RunId = runId });

        Assert.Equal(0, alertCount);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Aml_LowStrFiler_BelowPeerThreshold_RaisesAlert()
    {
        var runId = Guid.NewGuid();
        await _fx.SeedAmlMetricsAsync(
            new[]
            {
                (201, "DMB", 2),
                (202, "DMB", 18),
                (203, "DMB", 22),
                (204, "DMB", 20),
                (205, "DMB", 24)
            },
            "2026-Q1");

        var count = await _fx.Aml.DetectLowSTRFilersAsync("NFIU", "2026-Q1", runId);

        Assert.True(count >= 1);

        using var conn = await _fx.Db.CreateConnectionAsync(null);
        var alert = await conn.QuerySingleOrDefaultAsync<AlertRow>(
            """
            SELECT AlertCode, Severity
            FROM dbo.SurveillanceAlerts
            WHERE InstitutionId = 201
              AND AlertCode = 'AML_LOW_STR'
              AND DetectionRunId = @RunId
            """,
            new { RunId = runId });

        Assert.NotNull(alert);
        Assert.Contains(alert!.Severity, new[] { "HIGH", "CRITICAL" });
    }

    [Fact]
    public async Task Insurance_ClaimsSuppression_LowRatio_RaisesCriticalAlert()
    {
        var runId = Guid.NewGuid();
        await _fx.SeedInsuranceMetricsAsync(
            new[]
            {
                (301, 18m, 500_000m, 2_777_000m),
                (302, 55m, 1_100_000m, 2_000_000m),
                (303, 48m, 960_000m, 2_000_000m)
            },
            "2026-Q1");

        var count = await _fx.Insurance.DetectClaimsSuppressionAsync("NAICOM", "2026-Q1", runId);

        Assert.True(count >= 1);

        using var conn = await _fx.Db.CreateConnectionAsync(null);
        var alert = await conn.QuerySingleOrDefaultAsync<AlertRow>(
            """
            SELECT AlertCode, Severity
            FROM dbo.SurveillanceAlerts
            WHERE InstitutionId = 301
              AND AlertCode = 'INS_CLAIMS_SUPPRESSION'
              AND DetectionRunId = @RunId
            """,
            new { RunId = runId });

        Assert.NotNull(alert);
        Assert.Equal("CRITICAL", alert!.Severity);
    }

    [Fact]
    public async Task Whistleblower_Submit_StoresNoPiiAndReturnsToken()
    {
        var receipt = await _fx.Whistleblower.SubmitAsync(new WhistleblowerSubmission(
            "CBN",
            101,
            "Test BDC Alpha",
            WhistleblowerCategory.FxManipulation,
            "The institution has consistently quoted rates above the official CBN band and coordinated related-party trades to move market pricing.",
            "Transaction slips and screen captures exist.",
            Array.Empty<string>()));

        Assert.False(string.IsNullOrWhiteSpace(receipt.CaseReference));
        Assert.False(string.IsNullOrWhiteSpace(receipt.AnonymousToken));
        Assert.Equal(64, receipt.AnonymousToken.Length);

        using var conn = await _fx.Db.CreateConnectionAsync(null);
        var stored = await conn.QuerySingleAsync(
            """
            SELECT CaseReference, AnonymousToken, RegulatorCode, Category, Summary
            FROM dbo.WhistleblowerReports
            WHERE CaseReference = @CaseReference
            """,
            new { receipt.CaseReference });

        Assert.Equal("CBN", (string)stored.RegulatorCode);
        Assert.Equal("FxManipulation", (string)stored.Category);
        Assert.Equal(receipt.AnonymousToken, (string)stored.AnonymousToken);
    }

    [Fact]
    public async Task Whistleblower_StatusCheck_InvalidToken_ReturnsNull()
    {
        var status = await _fx.Whistleblower.CheckStatusAsync(new string('0', 64));
        Assert.Null(status);
    }

    [Fact]
    public async Task ConductRiskScorer_EntityWithOpenAlert_ProducesNonZeroScore()
    {
        var runId = Guid.NewGuid();
        using var conn = await _fx.Db.CreateConnectionAsync(null);
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.SurveillanceAlerts
                (TenantId, AlertCode, RegulatorCode, InstitutionId, Severity, Category, Title, DetectionRunId)
            VALUES
                (@TenantId, 'BDC_RATE_MANIPULATION', 'CBN', 401, 'HIGH', 'BDC_FX', 'Scoring seed alert', @RunId)
            """,
            new { TenantId = ConductRiskFixture.CbnRegulatorTenantId, RunId = runId });

        var score = await _fx.Scorer.ScoreInstitutionAsync(401, "CBN", "2026-Q1", runId);

        Assert.True(score.MarketAbuseScore > 0);
        Assert.True(score.CompositeScore > 0);
        Assert.NotEqual(ConductRiskBand.Low, score.RiskBand);
    }

    [Fact]
    public async Task Orchestrator_FullCycle_Completes()
    {
        await _fx.SeedBdcRatesAsync(101, 6, 1550m, 1565m, 1535m, 1585m, "2026-Q1");

        var result = await _fx.Orchestrator.RunCycleAsync("CBN", "2026-Q1");

        Assert.NotEqual(Guid.Empty, result.RunId);
        Assert.True(result.Duration > TimeSpan.Zero);
    }

    private sealed record AlertRow(string AlertCode, string Severity);
}

public sealed class ConductRiskFixture : IAsyncLifetime
{
    public static readonly Guid CbnRegulatorTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid NfiuRegulatorTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid NaicomRegulatorTenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid SecRegulatorTenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("C0nduct_R1sk_T3st!")
        .Build();

    public IDbConnectionFactory Db { get; private set; } = null!;
    public IBDCFXSurveillance Bdc { get; private set; } = null!;
    public IAMLConductMonitor Aml { get; private set; } = null!;
    public IInsuranceConductMonitor Insurance { get; private set; } = null!;
    public IConductRiskScorer Scorer { get; private set; } = null!;
    public IWhistleblowerService Whistleblower { get; private set; } = null!;
    public ISurveillanceOrchestrator Orchestrator { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        var cs = _sql.GetConnectionString();

        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await ApplySchemaAsync(conn);
        await SeedBaseDataAsync(conn);
        await SeedRuleParametersAsync(conn);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhistleblowerService:TokenSalt"] = "conduct-risk-test-salt-2026",
                ["RegOS:BaseUrl"] = "https://regos-test.local",
                ["ConductRiskSurveillance:RegulatorTenants:CBN"] = CbnRegulatorTenantId.ToString(),
                ["ConductRiskSurveillance:RegulatorTenants:NFIU"] = NfiuRegulatorTenantId.ToString(),
                ["ConductRiskSurveillance:RegulatorTenants:NAICOM"] = NaicomRegulatorTenantId.ToString(),
                ["ConductRiskSurveillance:RegulatorTenants:SEC"] = SecRegulatorTenantId.ToString()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());
        services.AddSingleton<IDbConnectionFactory>(new ConductRiskTestDbConnectionFactory(cs));
        services.AddSingleton<IConfiguration>(config);
        services.AddDbContext<MetadataDbContext>(options => options.UseSqlServer(cs));
        services.AddConductRiskSurveillance(config);

        var sp = services.BuildServiceProvider();
        Db = sp.GetRequiredService<IDbConnectionFactory>();
        Bdc = sp.GetRequiredService<IBDCFXSurveillance>();
        Aml = sp.GetRequiredService<IAMLConductMonitor>();
        Insurance = sp.GetRequiredService<IInsuranceConductMonitor>();
        Scorer = sp.GetRequiredService<IConductRiskScorer>();
        Whistleblower = sp.GetRequiredService<IWhistleblowerService>();
        Orchestrator = sp.GetRequiredService<ISurveillanceOrchestrator>();
    }

    public async Task DisposeAsync() => await _sql.DisposeAsync();

    public async Task SeedBdcRatesAsync(
        int institutionId,
        int daysOutside,
        decimal midRate,
        decimal bandUpper,
        decimal bandLower,
        decimal offendingRate,
        string periodCode)
    {
        using var conn = await Db.CreateConnectionAsync(null);
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
        for (var i = 0; i < 12; i++)
        {
            var date = start.AddDays(i);
            var rate = i >= 3 && i < 3 + daysOutside ? offendingRate : midRate;
            await conn.ExecuteAsync(
                """
                INSERT INTO dbo.BDCFXTransactions
                    (TenantId, InstitutionId, RegulatorCode, TransactionDate, PeriodCode, BuyRate, SellRate,
                     BuyVolumeUSD, SellVolumeUSD, CBNMidRate, CBNBandUpper, CBNBandLower)
                VALUES
                    (@TenantId, @InstitutionId, 'CBN', @TransactionDate, @PeriodCode, @Rate, @Rate,
                     500000, 500000, @MidRate, @BandUpper, @BandLower)
                """,
                new
                {
                    TenantId = CbnRegulatorTenantId,
                    InstitutionId = institutionId,
                    TransactionDate = date,
                    PeriodCode = periodCode,
                    Rate = rate,
                    MidRate = midRate,
                    BandUpper = bandUpper,
                    BandLower = bandLower
                });
        }
    }

    public async Task SeedAmlMetricsAsync((int InstitutionId, string InstitutionType, int StrCount)[] rows, string periodCode)
    {
        using var conn = await Db.CreateConnectionAsync(null);
        foreach (var row in rows)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO dbo.AMLConductMetrics
                    (TenantId, InstitutionId, RegulatorCode, InstitutionType, PeriodCode, AsOfDate, STRFilingCount, TFSScreeningCount)
                VALUES
                    (@TenantId, @InstitutionId, 'NFIU', @InstitutionType, @PeriodCode, CAST(SYSUTCDATETIME() AS date), @StrCount, 200)
                """,
                new
                {
                    TenantId = NfiuRegulatorTenantId,
                    row.InstitutionId,
                    row.InstitutionType,
                    PeriodCode = periodCode,
                    StrCount = row.StrCount
                });
        }
    }

    public async Task SeedInsuranceMetricsAsync((int InstitutionId, decimal ClaimsRatio, decimal GrossClaims, decimal GrossPremium)[] rows, string periodCode)
    {
        using var conn = await Db.CreateConnectionAsync(null);
        foreach (var row in rows)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO dbo.InsuranceConductMetrics
                    (TenantId, InstitutionId, RegulatorCode, InstitutionType, PeriodCode, AsOfDate,
                     ClaimsRatio, GrossClaimsNGN, GrossPremiumNGN, RelatedPartyReinsurancePct)
                VALUES
                    (@TenantId, @InstitutionId, 'NAICOM', 'INSURER', @PeriodCode, CAST(SYSUTCDATETIME() AS date),
                     @ClaimsRatio, @GrossClaims, @GrossPremium, 5.0)
                """,
                new
                {
                    TenantId = NaicomRegulatorTenantId,
                    row.InstitutionId,
                    PeriodCode = periodCode,
                    row.ClaimsRatio,
                    row.GrossClaims,
                    row.GrossPremium
                });
        }
    }

    private static async Task ApplySchemaAsync(SqlConnection conn)
    {
        await conn.ExecuteAsync("""
IF SCHEMA_ID('meta') IS NULL EXEC('CREATE SCHEMA meta');

IF OBJECT_ID(N'dbo.tenants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tenants (
        TenantId UNIQUEIDENTIFIER PRIMARY KEY,
        ParentTenantId UNIQUEIDENTIFIER NULL,
        TenantName NVARCHAR(200) NOT NULL,
        TenantSlug NVARCHAR(100) NOT NULL,
        TenantType NVARCHAR(30) NOT NULL,
        TenantStatus NVARCHAR(30) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID(N'dbo.institutions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.institutions (
        Id INT PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        InstitutionCode NVARCHAR(20) NOT NULL,
        InstitutionName NVARCHAR(255) NOT NULL,
        LicenseType NVARCHAR(100) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID(N'dbo.submission_items', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.submission_items (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        InstitutionId INT NOT NULL,
        RegulatorCode VARCHAR(10) NOT NULL,
        Status VARCHAR(20) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID(N'meta.supervisory_actions', N'U') IS NULL
BEGIN
    CREATE TABLE meta.supervisory_actions (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        InstitutionId INT NOT NULL,
        RegulatorCode VARCHAR(10) NOT NULL,
        ActionType VARCHAR(30) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID(N'meta.portal_users', N'U') IS NULL
BEGIN
    CREATE TABLE meta.portal_users (
        Id INT PRIMARY KEY,
        DisplayName NVARCHAR(200) NOT NULL
    );
END

IF OBJECT_ID(N'dbo.SurveillanceRuleParameters', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SurveillanceRuleParameters (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NULL,
        RuleCode VARCHAR(40) NOT NULL,
        RegulatorCode VARCHAR(10) NOT NULL,
        InstitutionType VARCHAR(20) NOT NULL DEFAULT 'ALL',
        ParamName VARCHAR(60) NOT NULL,
        ParamValue DECIMAL(18,6) NOT NULL,
        Description NVARCHAR(300) NULL,
        EffectiveFrom DATE NOT NULL DEFAULT CAST(GETUTCDATE() AS DATE),
        IsActive BIT NOT NULL DEFAULT 1
    );
END

IF OBJECT_ID(N'dbo.SurveillanceAlerts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SurveillanceAlerts (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AlertCode VARCHAR(40) NOT NULL,
        RegulatorCode VARCHAR(10) NOT NULL,
        InstitutionId INT NULL,
        Severity VARCHAR(10) NOT NULL,
        Category VARCHAR(20) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Detail NVARCHAR(2000) NULL,
        EvidenceJson NVARCHAR(MAX) NULL,
        PeriodCode VARCHAR(10) NULL,
        DetectionRunId UNIQUEIDENTIFIER NOT NULL,
        DetectedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID(N'dbo.SurveillanceAlertResolutions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SurveillanceAlertResolutions (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AlertId BIGINT NOT NULL,
        RegulatorCode VARCHAR(10) NOT NULL,
        ResolvedByUserId INT NOT NULL,
        ResolutionOutcome VARCHAR(20) NOT NULL,
        ResolutionNote NVARCHAR(500) NOT NULL,
        ResolvedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID(N'dbo.BDCFXTransactions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BDCFXTransactions (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        InstitutionId INT NOT NULL,
        RegulatorCode VARCHAR(10) NOT NULL,
        TransactionDate DATE NOT NULL,
        PeriodCode VARCHAR(10) NOT NULL,
        BuyCurrency VARCHAR(3) NOT NULL DEFAULT 'USD',
        SellCurrency VARCHAR(3) NOT NULL DEFAULT 'NGN',
        BuyRate DECIMAL(12,4) NOT NULL,
        SellRate DECIMAL(12,4) NOT NULL,
        BuyVolumeUSD DECIMAL(18,2) NOT NULL,
        SellVolumeUSD DECIMAL(18,2) NOT NULL,
        CBNMidRate DECIMAL(12,4) NOT NULL,
        CBNBandUpper DECIMAL(12,4) NOT NULL,
        CBNBandLower DECIMAL(12,4) NOT NULL,
        CounterpartyId INT NULL,
        SourceReturnId BIGINT NULL,
        CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID(N'dbo.CorporateAnnouncements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CorporateAnnouncements (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        RegulatorCode VARCHAR(10) NOT NULL,
        SecurityCode VARCHAR(20) NOT NULL,
        SecurityName NVARCHAR(200) NULL,
        AnnouncementType VARCHAR(40) NOT NULL,
        AnnouncementDate DATE NOT NULL,
        DisclosureDeadline DATE NULL,
        SourceReference NVARCHAR(300) NULL,
        CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID(N'dbo.CMOTradeReports', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CMOTradeReports (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        InstitutionId INT NOT NULL,
        RegulatorCode VARCHAR(10) NOT NULL,
        InstitutionType VARCHAR(20) NOT NULL,
        SecurityCode VARCHAR(20) NOT NULL,
        SecurityName NVARCHAR(200) NULL,
        TradeDate DATE NOT NULL,
        PeriodCode VARCHAR(10) NOT NULL,
        TradeType VARCHAR(10) NOT NULL,
        Quantity DECIMAL(18,2) NOT NULL,
        Price DECIMAL(14,4) NOT NULL,
        TradeValueNGN DECIMAL(18,2) NOT NULL,
        ClientId VARCHAR(64) NULL,
        ReportedAt DATETIME2(3) NOT NULL,
        TradeTimestamp DATETIME2(3) NOT NULL,
        IsLate BIT NOT NULL DEFAULT 0,
        SourceReturnId BIGINT NULL,
        CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID(N'dbo.AMLConductMetrics', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AMLConductMetrics (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        InstitutionId INT NOT NULL,
        RegulatorCode VARCHAR(10) NOT NULL,
        InstitutionType VARCHAR(20) NOT NULL,
        PeriodCode VARCHAR(10) NOT NULL,
        AsOfDate DATE NOT NULL,
        STRFilingCount INT NOT NULL DEFAULT 0,
        CTRFilingCount INT NOT NULL DEFAULT 0,
        PeerAvgSTRCount DECIMAL(8,2) NULL,
        STRDeviation DECIMAL(8,4) NULL,
        StructuringAlertCount INT NOT NULL DEFAULT 0,
        PEPAccountCount INT NOT NULL DEFAULT 0,
        PEPFlaggedActivityCount INT NOT NULL DEFAULT 0,
        TFSScreeningCount INT NOT NULL DEFAULT 0,
        TFSFalsePositiveRate DECIMAL(8,4) NULL,
        TFSTruePositiveCount INT NOT NULL DEFAULT 0,
        CustomerComplaintCount INT NOT NULL DEFAULT 0,
        ComplaintResolutionRate DECIMAL(8,4) NULL,
        SourceReturnId BIGINT NULL,
        CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID(N'dbo.InsuranceConductMetrics', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InsuranceConductMetrics (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        InstitutionId INT NOT NULL,
        RegulatorCode VARCHAR(10) NOT NULL,
        InstitutionType VARCHAR(20) NOT NULL DEFAULT 'INSURER',
        PeriodCode VARCHAR(10) NOT NULL,
        AsOfDate DATE NOT NULL,
        GrossClaimsNGN DECIMAL(18,2) NULL,
        GrossPremiumNGN DECIMAL(18,2) NULL,
        ClaimsRatio DECIMAL(8,4) NULL,
        PeerAvgClaimsRatio DECIMAL(8,4) NULL,
        GrossPremiumReported DECIMAL(18,2) NULL,
        GrossPremiumExpected DECIMAL(18,2) NULL,
        PremiumUnderReportingGap DECIMAL(18,2) NULL,
        ReinsuranceRecoveries DECIMAL(18,2) NULL,
        RelatedPartyReinsurancePct DECIMAL(8,4) NULL,
        ComplaintCount INT NOT NULL DEFAULT 0,
        ClaimsDenialRate DECIMAL(8,4) NULL,
        SourceReturnId BIGINT NULL,
        CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID(N'dbo.ConductRiskScores', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConductRiskScores (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        InstitutionId INT NOT NULL,
        RegulatorCode VARCHAR(10) NOT NULL,
        InstitutionType VARCHAR(20) NOT NULL,
        PeriodCode VARCHAR(10) NOT NULL,
        ScoreVersion INT NOT NULL DEFAULT 1,
        MarketAbuseScore DECIMAL(5,2) NOT NULL DEFAULT 0,
        AMLEffectivenessScore DECIMAL(5,2) NOT NULL DEFAULT 0,
        InsuranceConductScore DECIMAL(5,2) NOT NULL DEFAULT 0,
        CustomerConductScore DECIMAL(5,2) NOT NULL DEFAULT 0,
        GovernanceScore DECIMAL(5,2) NOT NULL DEFAULT 0,
        SanctionHistoryScore DECIMAL(5,2) NOT NULL DEFAULT 0,
        CompositeScore DECIMAL(5,2) NOT NULL,
        RiskBand VARCHAR(10) NOT NULL,
        ActiveAlertCount INT NOT NULL DEFAULT 0,
        ComputationRunId UNIQUEIDENTIFIER NOT NULL,
        ComputedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID(N'dbo.WhistleblowerReports', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WhistleblowerReports (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CaseReference VARCHAR(30) NOT NULL,
        AnonymousToken VARCHAR(64) NOT NULL,
        RegulatorCode VARCHAR(10) NOT NULL,
        AllegedInstitutionId INT NULL,
        AllegedInstitutionName NVARCHAR(200) NULL,
        Category VARCHAR(30) NOT NULL,
        Summary NVARCHAR(2000) NOT NULL,
        EvidenceDescription NVARCHAR(1000) NULL,
        EvidenceS3Keys NVARCHAR(MAX) NULL,
        Status VARCHAR(20) NOT NULL DEFAULT 'RECEIVED',
        AssignedToUserId INT NULL,
        PriorityScore INT NOT NULL DEFAULT 50,
        ReceivedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

IF OBJECT_ID(N'dbo.WhistleblowerCaseEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WhistleblowerCaseEvents (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        RegulatorCode VARCHAR(10) NOT NULL,
        WhistleblowerReportId BIGINT NOT NULL,
        EventType VARCHAR(30) NOT NULL,
        Note NVARCHAR(1000) NULL,
        PerformedByUserId INT NULL,
        PerformedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
""");
    }

    private static async Task SeedBaseDataAsync(SqlConnection conn)
    {
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.tenants (TenantId, TenantName, TenantSlug, TenantType, TenantStatus)
            VALUES
                (@CbnTenantId, 'CBN Regulator', 'cbn-regulator', 'Regulator', 'Active'),
                (@NfiuTenantId, 'NFIU Regulator', 'nfiu-regulator', 'Regulator', 'Active'),
                (@NaicomTenantId, 'NAICOM Regulator', 'naicom-regulator', 'Regulator', 'Active'),
                (@SecTenantId, 'SEC Regulator', 'sec-regulator', 'Regulator', 'Active');

            INSERT INTO dbo.institutions (Id, TenantId, InstitutionCode, InstitutionName, LicenseType, IsActive)
            VALUES
                (101, @CbnTenantId, 'BDC-101', 'Test BDC Alpha', 'BDC', 1),
                (102, @CbnTenantId, 'BDC-102', 'Test BDC Beta', 'BDC', 1),
                (201, @NfiuTenantId, 'DMB-201', 'Low STR Bank', 'DMB', 1),
                (202, @NfiuTenantId, 'DMB-202', 'Normal Bank A', 'DMB', 1),
                (203, @NfiuTenantId, 'DMB-203', 'Normal Bank B', 'DMB', 1),
                (204, @NfiuTenantId, 'DMB-204', 'Normal Bank C', 'DMB', 1),
                (205, @NfiuTenantId, 'DMB-205', 'Normal Bank D', 'DMB', 1),
                (301, @NaicomTenantId, 'INS-301', 'Insurer Alpha', 'INSURER', 1),
                (302, @NaicomTenantId, 'INS-302', 'Insurer Beta', 'INSURER', 1),
                (303, @NaicomTenantId, 'INS-303', 'Insurer Gamma', 'INSURER', 1),
                (401, @CbnTenantId, 'DMB-401', 'Scorer Test Bank', 'DMB', 1);

            INSERT INTO meta.portal_users (Id, DisplayName)
            VALUES (1, 'Supervisor One');

            INSERT INTO dbo.submission_items (InstitutionId, RegulatorCode, Status, CreatedAt)
            VALUES
                (101, 'CBN', 'SUBMITTED', SYSUTCDATETIME()),
                (401, 'CBN', 'OVERDUE', DATEADD(DAY, -30, SYSUTCDATETIME()));
            """,
            new
            {
                CbnTenantId = CbnRegulatorTenantId,
                NfiuTenantId = NfiuRegulatorTenantId,
                NaicomTenantId = NaicomRegulatorTenantId,
                SecTenantId = SecRegulatorTenantId
            });
    }

    private static async Task SeedRuleParametersAsync(SqlConnection conn)
    {
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.SurveillanceRuleParameters
                (TenantId, RuleCode, RegulatorCode, InstitutionType, ParamName, ParamValue)
            VALUES
                (NULL, 'BDC_RATE_MANIPULATION', 'CBN', 'BDC', 'ConsecutiveDaysOutsideBand', 5),
                (NULL, 'BDC_RATE_MANIPULATION', 'CBN', 'BDC', 'BandTolerancePct', 1.5),
                (NULL, 'BDC_VOLUME_SPIKE', 'CBN', 'BDC', 'VolumeZScoreThreshold', 3.0),
                (NULL, 'BDC_VOLUME_SPIKE', 'CBN', 'BDC', 'LookbackDays', 30),
                (NULL, 'BDC_WASH_TRADE', 'CBN', 'BDC', 'CircularTxnWindowDays', 7),
                (NULL, 'BDC_WASH_TRADE', 'CBN', 'BDC', 'MinCircularAmountUSD', 50000),
                (NULL, 'AML_LOW_STR', 'NFIU', 'ALL', 'STRZScoreThreshold', -2.0),
                (NULL, 'AML_STRUCTURING', 'NFIU', 'ALL', 'CTRThresholdNGN', 5000000),
                (NULL, 'AML_STRUCTURING', 'NFIU', 'ALL', 'StructuringWindowDays', 3),
                (NULL, 'AML_TFS_FALSE_POS', 'NFIU', 'ALL', 'TFSFalsePositiveRateMax', 0.95),
                (NULL, 'AML_TFS_FALSE_POS', 'NFIU', 'ALL', 'TFSFalsePositiveRateMin', 0.05),
                (NULL, 'INS_CLAIMS_SUPPRESSION', 'NAICOM', 'INSURER', 'MinClaimsRatioPct', 30),
                (NULL, 'INS_CLAIMS_SUPPRESSION', 'NAICOM', 'INSURER', 'PeerDeviation', 20),
                (NULL, 'INS_PREMIUM_UNDER', 'NAICOM', 'INSURER', 'PremiumGapThresholdPct', 15),
                (NULL, 'INS_RELATED_REINS', 'NAICOM', 'INSURER', 'RelatedPartyReinsCapPct', 30),
                (NULL, 'CMO_UNUSUAL_TRADE', 'SEC', 'CMO', 'PreAnnouncementWindowDays', 3),
                (NULL, 'CMO_UNUSUAL_TRADE', 'SEC', 'CMO', 'VolumeMultiplierThreshold', 5),
                (NULL, 'CMO_LATE_REPORT', 'SEC', 'CMO', 'MaxReportingDelayHours', 24),
                (NULL, 'CMO_CONCENTRATION', 'SEC', 'CMO', 'SingleSecurityConcentrationPct', 25);
            """);
    }
}

internal sealed class ConductRiskTestDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public ConductRiskTestDbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IDbConnection> CreateConnectionAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
