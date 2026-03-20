#nullable enable
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FC.Engine.Infrastructure.Metadata.Migrations;

/// <summary>
/// RG-12: Filing Calendar, Deadline Engine & Return Lifecycle.
/// Extends return_periods with module awareness, deadline tracking, status, and notification levels.
/// Adds DeadlineOffsetDays to modules. Creates filing_sla_records table.
/// Applies RLS to new table.
/// </summary>
[DbContext(typeof(MetadataDbContext))]
[Migration("20260305200000_AddFilingCalendarRg12")]
public partial class AddFilingCalendarRg12 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ═══════════════════════════════════════════════════════════
        // STEP 1: Extend return_periods table
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            ALTER TABLE dbo.return_periods ADD
                ModuleId               INT             NULL,
                Quarter                INT             NULL,
                DeadlineDate           DATETIME2       NOT NULL DEFAULT '9999-12-31',
                DeadlineOverrideDate   DATETIME2       NULL,
                DeadlineOverrideBy     INT             NULL,
                DeadlineOverrideReason NVARCHAR(500)   NULL,
                AutoCreatedReturnId    INT             NULL,
                Status                 NVARCHAR(20)    NOT NULL DEFAULT 'Upcoming',
                NotificationLevel      INT             NOT NULL DEFAULT 0;

            -- FK to Modules
            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'modules' AND schema_id = SCHEMA_ID('meta'))
            BEGIN
                ALTER TABLE dbo.return_periods
                    ADD CONSTRAINT FK_return_periods_Modules_ModuleId
                    FOREIGN KEY (ModuleId) REFERENCES meta.modules(Id);
            END

            -- FK to return_submissions (auto-created draft)
            ALTER TABLE dbo.return_periods
                ADD CONSTRAINT FK_return_periods_Submissions_AutoCreatedReturnId
                FOREIGN KEY (AutoCreatedReturnId) REFERENCES dbo.return_submissions(Id);

            -- Composite index for period lookup
            CREATE NONCLUSTERED INDEX IX_return_periods_TenantId_ModuleId_Year_Month_Quarter
                ON dbo.return_periods (TenantId, ModuleId, Year, Month, Quarter);
        ");

        // ═══════════════════════════════════════════════════════════
        // STEP 2: Add DeadlineOffsetDays to modules
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.modules') AND name = 'DeadlineOffsetDays')
            BEGIN
                ALTER TABLE dbo.modules ADD DeadlineOffsetDays INT NULL;
            END
        ");

        // ═══════════════════════════════════════════════════════════
        // STEP 3: Create filing_sla_records table
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            CREATE TABLE dbo.filing_sla_records (
                Id              INT                 IDENTITY(1,1)  PRIMARY KEY,
                TenantId        UNIQUEIDENTIFIER    NOT NULL       REFERENCES dbo.tenants(TenantId),
                ModuleId        INT                 NOT NULL,
                PeriodId        INT                 NOT NULL       REFERENCES dbo.return_periods(Id),
                SubmissionId    INT                 NULL           REFERENCES dbo.return_submissions(Id),
                PeriodEndDate   DATETIME2           NOT NULL,
                DeadlineDate    DATETIME2           NOT NULL,
                SubmittedDate   DATETIME2           NULL,
                DaysToDeadline  INT                 NULL,
                OnTime          BIT                 NULL,

                CONSTRAINT UQ_filing_sla_records_TenantModulePeriod
                    UNIQUE (TenantId, ModuleId, PeriodId)
            );

            -- FK to Modules (conditional on schema)
            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'modules' AND schema_id = SCHEMA_ID('meta'))
            BEGIN
                ALTER TABLE dbo.filing_sla_records
                    ADD CONSTRAINT FK_filing_sla_records_Modules_ModuleId
                    FOREIGN KEY (ModuleId) REFERENCES meta.modules(Id);
            END

            CREATE NONCLUSTERED INDEX IX_filing_sla_records_TenantId
                ON dbo.filing_sla_records (TenantId);
        ");

        // ═══════════════════════════════════════════════════════════
        // STEP 4: Update RLS security policy to include new table
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            -- Drop and recreate the security policy to include the new table
            IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = 'TenantSecurityPolicy')
            BEGIN
                DROP SECURITY POLICY dbo.TenantSecurityPolicy;
            END

            DECLARE @policySql NVARCHAR(MAX) = 'CREATE SECURITY POLICY dbo.TenantSecurityPolicy' + CHAR(13);
            DECLARE @first BIT = 1;

            -- Build FILTER predicates for all tables with TenantId column
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

            -- Add BLOCK predicates for all tables with TenantId column
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
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // ═══════════════════════════════════════════════════════════
        // REVERSE STEP 4: Rebuild RLS without filing_sla_records
        // (will be handled by the same dynamic rebuild)
        // ═══════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════
        // REVERSE STEP 3: Drop filing_sla_records
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = 'TenantSecurityPolicy')
            BEGIN
                DROP SECURITY POLICY dbo.TenantSecurityPolicy;
            END

            IF OBJECT_ID(N'dbo.filing_sla_records', N'U') IS NOT NULL
            BEGIN
                DROP TABLE dbo.filing_sla_records;
            END
        ");

        // ═══════════════════════════════════════════════════════════
        // REVERSE STEP 2: Remove DeadlineOffsetDays from modules
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.modules') AND name = 'DeadlineOffsetDays')
            BEGIN
                ALTER TABLE dbo.modules DROP COLUMN DeadlineOffsetDays;
            END
        ");

        // ═══════════════════════════════════════════════════════════
        // REVERSE STEP 1: Remove columns from return_periods
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            -- Drop indexes
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_return_periods_TenantId_ModuleId_Year_Month_Quarter' AND object_id = OBJECT_ID(N'dbo.return_periods'))
                DROP INDEX IX_return_periods_TenantId_ModuleId_Year_Month_Quarter ON dbo.return_periods;

            -- Drop FKs
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_return_periods_Submissions_AutoCreatedReturnId')
                ALTER TABLE dbo.return_periods DROP CONSTRAINT FK_return_periods_Submissions_AutoCreatedReturnId;

            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_return_periods_Modules_ModuleId')
                ALTER TABLE dbo.return_periods DROP CONSTRAINT FK_return_periods_Modules_ModuleId;

            -- Drop default constraints
            DECLARE @defName NVARCHAR(256);

            SELECT @defName = d.name FROM sys.default_constraints d
            INNER JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
            WHERE d.parent_object_id = OBJECT_ID(N'dbo.return_periods') AND c.name = 'DeadlineDate';
            IF @defName IS NOT NULL EXEC('ALTER TABLE dbo.return_periods DROP CONSTRAINT ' + @defName);

            SELECT @defName = d.name FROM sys.default_constraints d
            INNER JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
            WHERE d.parent_object_id = OBJECT_ID(N'dbo.return_periods') AND c.name = 'Status';
            IF @defName IS NOT NULL EXEC('ALTER TABLE dbo.return_periods DROP CONSTRAINT ' + @defName);

            SELECT @defName = d.name FROM sys.default_constraints d
            INNER JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
            WHERE d.parent_object_id = OBJECT_ID(N'dbo.return_periods') AND c.name = 'NotificationLevel';
            IF @defName IS NOT NULL EXEC('ALTER TABLE dbo.return_periods DROP CONSTRAINT ' + @defName);

            -- Drop columns
            ALTER TABLE dbo.return_periods DROP COLUMN
                ModuleId, Quarter, DeadlineDate, DeadlineOverrideDate,
                DeadlineOverrideBy, DeadlineOverrideReason, AutoCreatedReturnId,
                Status, NotificationLevel;
        ");

        // Rebuild RLS policy without the dropped table
        migrationBuilder.Sql(@"
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
        ");
    }
}
