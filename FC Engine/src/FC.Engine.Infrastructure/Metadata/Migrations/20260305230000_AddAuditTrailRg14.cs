#nullable enable
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FC.Engine.Infrastructure.Metadata.Migrations;

[DbContext(typeof(MetadataDbContext))]
[Migration("20260305230000_AddAuditTrailRg14")]
public partial class AddAuditTrailRg14 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Add hash chain columns to existing audit_log table
        migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('meta.audit_log') AND name = 'Hash')
            BEGIN
                ALTER TABLE meta.audit_log ADD Hash NVARCHAR(64) NOT NULL DEFAULT '';
                ALTER TABLE meta.audit_log ADD PreviousHash NVARCHAR(64) NOT NULL DEFAULT 'GENESIS';
                ALTER TABLE meta.audit_log ADD SequenceNumber BIGINT NOT NULL DEFAULT 0;
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_audit_log_TenantId_SequenceNumber' AND object_id = OBJECT_ID('meta.audit_log'))
                CREATE UNIQUE INDEX IX_audit_log_TenantId_SequenceNumber
                    ON meta.audit_log(TenantId, SequenceNumber)
                    WHERE SequenceNumber > 0;
        ");

        // 2. Create field_change_history table
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.field_change_history', N'U') IS NULL
            BEGIN
                CREATE TABLE meta.field_change_history (
                    Id              BIGINT              IDENTITY(1,1) PRIMARY KEY,
                    TenantId        UNIQUEIDENTIFIER    NOT NULL,
                    SubmissionId    INT                 NOT NULL,
                    ReturnCode      NVARCHAR(20)        NOT NULL,
                    FieldName       NVARCHAR(100)       NOT NULL,
                    OldValue        NVARCHAR(MAX)       NULL,
                    NewValue        NVARCHAR(MAX)       NULL,
                    ChangeSource    NVARCHAR(20)        NOT NULL DEFAULT 'Manual',
                    SourceDetail    NVARCHAR(200)       NULL,
                    ChangedBy       NVARCHAR(100)       NOT NULL,
                    ChangedAt       DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME()
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_field_change_history_TenantId_SubmissionId_FieldName' AND object_id = OBJECT_ID('meta.field_change_history'))
                CREATE INDEX IX_field_change_history_TenantId_SubmissionId_FieldName
                    ON meta.field_change_history(TenantId, SubmissionId, FieldName);
        ");

        // 3. Create evidence_packages table
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.evidence_packages', N'U') IS NULL
            BEGIN
                CREATE TABLE meta.evidence_packages (
                    Id              INT                 IDENTITY(1,1) PRIMARY KEY,
                    TenantId        UNIQUEIDENTIFIER    NOT NULL,
                    SubmissionId    INT                 NOT NULL,
                    PackageHash     NVARCHAR(64)        NOT NULL,
                    StoragePath     NVARCHAR(500)       NOT NULL,
                    FileSizeBytes   BIGINT              NOT NULL,
                    GeneratedAt     DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
                    GeneratedBy     NVARCHAR(100)       NOT NULL
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_evidence_packages_TenantId_SubmissionId' AND object_id = OBJECT_ID('meta.evidence_packages'))
                CREATE INDEX IX_evidence_packages_TenantId_SubmissionId
                    ON meta.evidence_packages(TenantId, SubmissionId);
        ");

        // 4. Rebuild RLS security policy to include new tables
        RebuildTenantSecurityPolicy(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.evidence_packages', N'U') IS NOT NULL
                DROP TABLE meta.evidence_packages;

            IF OBJECT_ID(N'meta.field_change_history', N'U') IS NOT NULL
                DROP TABLE meta.field_change_history;

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_audit_log_TenantId_SequenceNumber' AND object_id = OBJECT_ID('meta.audit_log'))
                DROP INDEX IX_audit_log_TenantId_SequenceNumber ON meta.audit_log;

            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('meta.audit_log') AND name = 'Hash')
            BEGIN
                ALTER TABLE meta.audit_log DROP COLUMN Hash;
                ALTER TABLE meta.audit_log DROP COLUMN PreviousHash;
                ALTER TABLE meta.audit_log DROP COLUMN SequenceNumber;
            END;
        ");

        RebuildTenantSecurityPolicy(migrationBuilder);
    }

    private static void RebuildTenantSecurityPolicy(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'dbo.fn_TenantFilter', N'IF') IS NULL
            BEGIN
                EXEC('
                    CREATE FUNCTION dbo.fn_TenantFilter(@TenantId UNIQUEIDENTIFIER)
                    RETURNS TABLE
                    WITH SCHEMABINDING
                    AS
                    RETURN
                    SELECT 1 AS fn_accessResult
                    WHERE @TenantId = CAST(SESSION_CONTEXT(N''TenantId'') AS UNIQUEIDENTIFIER)
                       OR @TenantId IS NULL
                       OR SESSION_CONTEXT(N''TenantId'') IS NULL;');
            END;

            IF OBJECT_ID(N'dbo.TenantSecurityPolicy', N'SP') IS NOT NULL
                DROP SECURITY POLICY dbo.TenantSecurityPolicy;

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
        ");
    }
}
