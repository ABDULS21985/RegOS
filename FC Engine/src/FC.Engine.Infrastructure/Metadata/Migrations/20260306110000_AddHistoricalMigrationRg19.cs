#nullable enable
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FC.Engine.Infrastructure.Metadata.Migrations;

[DbContext(typeof(MetadataDbContext))]
[Migration("20260306110000_AddHistoricalMigrationRg19")]
public partial class AddHistoricalMigrationRg19 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'dbo.import_mappings', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.import_mappings (
                    Id                INT               IDENTITY(1,1) PRIMARY KEY,
                    TenantId          UNIQUEIDENTIFIER  NOT NULL REFERENCES dbo.tenants(TenantId),
                    InstitutionId     INT               NOT NULL,
                    TemplateId        INT               NOT NULL REFERENCES dbo.return_templates(Id),
                    SourceFormat      NVARCHAR(20)      NOT NULL,
                    SourceIdentifier  NVARCHAR(200)     NULL,
                    MappingConfig     NVARCHAR(MAX)     NOT NULL,
                    CreatedAt         DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
                    UpdatedAt         DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT UQ_import_mappings UNIQUE (TenantId, InstitutionId, TemplateId, SourceFormat)
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_import_mappings_TenantId' AND object_id = OBJECT_ID('dbo.import_mappings'))
                CREATE INDEX IX_import_mappings_TenantId ON dbo.import_mappings(TenantId);

            IF OBJECT_ID(N'dbo.import_jobs', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.import_jobs (
                    Id                INT               IDENTITY(1,1) PRIMARY KEY,
                    TenantId          UNIQUEIDENTIFIER  NOT NULL REFERENCES dbo.tenants(TenantId),
                    TemplateId        INT               NOT NULL REFERENCES dbo.return_templates(Id),
                    InstitutionId     INT               NOT NULL,
                    ReturnPeriodId    INT               NULL REFERENCES dbo.return_periods(Id),
                    SourceFileName    NVARCHAR(255)     NOT NULL,
                    SourceFormat      NVARCHAR(20)      NOT NULL,
                    Status            NVARCHAR(20)      NOT NULL DEFAULT 'Uploaded',
                    RecordCount       INT               NULL,
                    ErrorCount        INT               NULL,
                    WarningCount      INT               NULL,
                    StagedData        NVARCHAR(MAX)     NULL,
                    ValidationReport  NVARCHAR(MAX)     NULL,
                    ImportedBy        INT               NOT NULL,
                    CreatedAt         DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
                    UpdatedAt         DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME()
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_import_jobs_TenantId' AND object_id = OBJECT_ID('dbo.import_jobs'))
                CREATE INDEX IX_import_jobs_TenantId ON dbo.import_jobs(TenantId);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_import_jobs_Tenant_Status_CreatedAt' AND object_id = OBJECT_ID('dbo.import_jobs'))
                CREATE INDEX IX_import_jobs_Tenant_Status_CreatedAt ON dbo.import_jobs(TenantId, Status, CreatedAt);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_import_jobs_Tenant_Template_CreatedAt' AND object_id = OBJECT_ID('dbo.import_jobs'))
                CREATE INDEX IX_import_jobs_Tenant_Template_CreatedAt ON dbo.import_jobs(TenantId, TemplateId, CreatedAt);

            IF OBJECT_ID(N'dbo.migration_module_signoffs', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.migration_module_signoffs (
                    Id                INT               IDENTITY(1,1) PRIMARY KEY,
                    TenantId          UNIQUEIDENTIFIER  NOT NULL REFERENCES dbo.tenants(TenantId),
                    ModuleId          INT               NOT NULL REFERENCES dbo.modules(Id),
                    IsSignedOff       BIT               NOT NULL DEFAULT 0,
                    SignedOffBy       INT               NOT NULL,
                    SignedOffAt       DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
                    Notes             NVARCHAR(1000)    NULL,
                    CONSTRAINT UQ_migration_module_signoffs UNIQUE (TenantId, ModuleId)
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_migration_module_signoffs_TenantId' AND object_id = OBJECT_ID('dbo.migration_module_signoffs'))
                CREATE INDEX IX_migration_module_signoffs_TenantId ON dbo.migration_module_signoffs(TenantId);
        ");

        RebuildTenantSecurityPolicy(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'dbo.import_jobs', N'U') IS NOT NULL
                DROP TABLE dbo.import_jobs;

            IF OBJECT_ID(N'dbo.import_mappings', N'U') IS NOT NULL
                DROP TABLE dbo.import_mappings;

            IF OBJECT_ID(N'dbo.migration_module_signoffs', N'U') IS NOT NULL
                DROP TABLE dbo.migration_module_signoffs;
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
