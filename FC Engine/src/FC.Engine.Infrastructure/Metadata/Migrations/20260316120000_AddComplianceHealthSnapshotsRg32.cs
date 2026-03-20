#nullable enable
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FC.Engine.Infrastructure.Metadata.Migrations;

/// <summary>
/// RG-32: Compliance Health Scoring — creates chs_score_snapshots table
/// for persisted weekly CHS snapshots used by the Compliance Health dashboard.
/// </summary>
[DbContext(typeof(MetadataDbContext))]
[Migration("20260316120000_AddComplianceHealthSnapshotsRg32")]
public partial class AddComplianceHealthSnapshotsRg32 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ═══════════════════════════════════════════════════════════
        // STEP 1: Create chs_score_snapshots table
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'dbo.chs_score_snapshots', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.chs_score_snapshots (
                    Id                  BIGINT              IDENTITY(1,1)  PRIMARY KEY,
                    TenantId            UNIQUEIDENTIFIER    NOT NULL       REFERENCES dbo.tenants(TenantId),
                    PeriodLabel         NVARCHAR(20)        NOT NULL,
                    ComputedAt          DATETIME2           NOT NULL,
                    OverallScore        DECIMAL(5,2)        NOT NULL,
                    Rating              INT                 NOT NULL,
                    FilingTimeliness    DECIMAL(5,2)        NOT NULL  DEFAULT 0,
                    DataQuality         DECIMAL(5,2)        NOT NULL  DEFAULT 0,
                    RegulatoryCapital   DECIMAL(5,2)        NOT NULL  DEFAULT 0,
                    AuditGovernance     DECIMAL(5,2)        NOT NULL  DEFAULT 0,
                    Engagement          DECIMAL(5,2)        NOT NULL  DEFAULT 0,

                    CONSTRAINT UQ_chs_score_snapshots_TenantPeriod
                        UNIQUE (TenantId, PeriodLabel)
                );

                CREATE NONCLUSTERED INDEX IX_chs_score_snapshots_TenantId
                    ON dbo.chs_score_snapshots (TenantId);

                CREATE NONCLUSTERED INDEX IX_chs_score_snapshots_ComputedAt
                    ON dbo.chs_score_snapshots (ComputedAt);
            END
        ");

        // ═══════════════════════════════════════════════════════════
        // STEP 2: Update RLS security policy to include new table
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
        migrationBuilder.Sql(@"
            -- Drop RLS policy first, then rebuild without the table
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

            IF OBJECT_ID(N'dbo.chs_score_snapshots', N'U') IS NOT NULL
            BEGIN
                DROP TABLE dbo.chs_score_snapshots;
            END

            -- Rebuild RLS without the dropped table
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
