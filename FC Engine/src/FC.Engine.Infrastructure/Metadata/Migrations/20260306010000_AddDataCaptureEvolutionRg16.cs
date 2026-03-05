#nullable enable
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FC.Engine.Infrastructure.Metadata.Migrations;

[DbContext(typeof(MetadataDbContext))]
[Migration("20260306010000_AddDataCaptureEvolutionRg16")]
public partial class AddDataCaptureEvolutionRg16 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'dbo.return_locks', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.return_locks (
                    Id              INT             IDENTITY(1,1) PRIMARY KEY,
                    TenantId        UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.tenants(TenantId),
                    SubmissionId    INT             NOT NULL REFERENCES dbo.return_submissions(Id),
                    UserId          INT             NOT NULL,
                    UserName        NVARCHAR(100)   NOT NULL,
                    LockedAt        DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
                    ExpiresAt       DATETIME2       NOT NULL,
                    HeartbeatAt     DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT UQ_return_locks_SubmissionId UNIQUE (SubmissionId)
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_return_locks_TenantId' AND object_id = OBJECT_ID('dbo.return_locks'))
                CREATE INDEX IX_return_locks_TenantId ON dbo.return_locks(TenantId);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_return_locks_ExpiresAt' AND object_id = OBJECT_ID('dbo.return_locks'))
                CREATE INDEX IX_return_locks_ExpiresAt ON dbo.return_locks(ExpiresAt);

            IF OBJECT_ID(N'dbo.data_feed_request_logs', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.data_feed_request_logs (
                    Id               INT              IDENTITY(1,1) PRIMARY KEY,
                    TenantId         UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.tenants(TenantId),
                    ReturnCode       NVARCHAR(50)     NOT NULL,
                    IdempotencyKey   NVARCHAR(150)    NOT NULL,
                    RequestHash      NVARCHAR(64)     NOT NULL,
                    ResultJson       NVARCHAR(MAX)    NOT NULL,
                    CreatedAt        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT UQ_data_feed_request_logs_TenantKey UNIQUE (TenantId, IdempotencyKey)
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_data_feed_request_logs_TenantId' AND object_id = OBJECT_ID('dbo.data_feed_request_logs'))
                CREATE INDEX IX_data_feed_request_logs_TenantId ON dbo.data_feed_request_logs(TenantId);

            IF OBJECT_ID(N'dbo.tenant_field_mappings', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.tenant_field_mappings (
                    Id                INT              IDENTITY(1,1) PRIMARY KEY,
                    TenantId          UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.tenants(TenantId),
                    IntegrationName   NVARCHAR(80)     NOT NULL,
                    ReturnCode        NVARCHAR(50)     NOT NULL,
                    ExternalFieldName NVARCHAR(128)    NOT NULL,
                    TemplateFieldName NVARCHAR(128)    NOT NULL,
                    IsActive          BIT              NOT NULL DEFAULT 1,
                    CreatedAt         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
                    UpdatedAt         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT UQ_tenant_field_mappings UNIQUE
                        (TenantId, IntegrationName, ReturnCode, ExternalFieldName)
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tenant_field_mappings_TenantId' AND object_id = OBJECT_ID('dbo.tenant_field_mappings'))
                CREATE INDEX IX_tenant_field_mappings_TenantId ON dbo.tenant_field_mappings(TenantId);
        ");

        RebuildTenantSecurityPolicy(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'dbo.tenant_field_mappings', N'U') IS NOT NULL
                DROP TABLE dbo.tenant_field_mappings;

            IF OBJECT_ID(N'dbo.data_feed_request_logs', N'U') IS NOT NULL
                DROP TABLE dbo.data_feed_request_logs;

            IF OBJECT_ID(N'dbo.return_locks', N'U') IS NOT NULL
                DROP TABLE dbo.return_locks;
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
