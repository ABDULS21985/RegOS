#nullable enable
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FC.Engine.Infrastructure.Metadata.Migrations;

[DbContext(typeof(MetadataDbContext))]
[Migration("20260306180000_AddWhiteLabelPartnerPortalRg26")]
public partial class AddWhiteLabelPartnerPortalRg26 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF COL_LENGTH('dbo.tenants', 'ParentTenantId') IS NULL
                ALTER TABLE dbo.tenants ADD ParentTenantId UNIQUEIDENTIFIER NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_tenants_ParentTenant')
                ALTER TABLE dbo.tenants
                    ADD CONSTRAINT FK_tenants_ParentTenant
                    FOREIGN KEY (ParentTenantId)
                    REFERENCES dbo.tenants(TenantId);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tenants_ParentTenantId' AND object_id = OBJECT_ID('dbo.tenants'))
                CREATE INDEX IX_tenants_ParentTenantId ON dbo.tenants(ParentTenantId);

            IF OBJECT_ID(N'dbo.partner_configs', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.partner_configs (
                    Id                INT                 IDENTITY(1,1) PRIMARY KEY,
                    TenantId          UNIQUEIDENTIFIER    NOT NULL UNIQUE REFERENCES dbo.tenants(TenantId),
                    PartnerTier       NVARCHAR(20)        NOT NULL DEFAULT 'Silver',
                    BillingModel      NVARCHAR(20)        NOT NULL DEFAULT 'Direct',
                    CommissionRate    DECIMAL(5,4)        NULL,
                    WholesaleDiscount DECIMAL(5,4)        NULL,
                    MaxSubTenants     INT                 NOT NULL DEFAULT 10,
                    AgreementSignedAt DATETIME2           NULL,
                    AgreementVersion  NVARCHAR(20)        NULL,
                    CreatedAt         DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
                    UpdatedAt         DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME()
                );
            END;

            IF OBJECT_ID(N'dbo.partner_revenue_records', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.partner_revenue_records (
                    Id                      INT                 IDENTITY(1,1) PRIMARY KEY,
                    TenantId                UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
                    PartnerTenantId         UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
                    InvoiceId               INT                 NOT NULL REFERENCES dbo.invoices(Id) ON DELETE CASCADE,
                    BillingModel            NVARCHAR(20)        NOT NULL,
                    GrossAmount             DECIMAL(18,2)       NOT NULL,
                    NetAmount               DECIMAL(18,2)       NOT NULL,
                    CommissionRate          DECIMAL(5,4)        NULL,
                    CommissionAmount        DECIMAL(18,2)       NOT NULL DEFAULT 0,
                    WholesaleDiscountRate   DECIMAL(5,4)        NULL,
                    WholesaleDiscountAmount DECIMAL(18,2)       NOT NULL DEFAULT 0,
                    PeriodStart             DATE                NOT NULL,
                    PeriodEnd               DATE                NOT NULL,
                    CreatedAt               DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT UQ_partner_revenue_records_PartnerTenant_Invoice UNIQUE (PartnerTenantId, InvoiceId)
                );
            END;

            IF OBJECT_ID(N'dbo.partner_support_tickets', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.partner_support_tickets (
                    Id                INT                 IDENTITY(1,1) PRIMARY KEY,
                    TenantId          UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
                    PartnerTenantId   UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
                    RaisedByUserId    INT                 NOT NULL,
                    RaisedByUserName  NVARCHAR(100)       NOT NULL,
                    Title             NVARCHAR(200)       NOT NULL,
                    Description       NVARCHAR(2000)      NOT NULL,
                    Priority          NVARCHAR(20)        NOT NULL DEFAULT 'Normal',
                    Status            NVARCHAR(20)        NOT NULL DEFAULT 'Open',
                    EscalationLevel   INT                 NOT NULL DEFAULT 0,
                    EscalatedAt       DATETIME2           NULL,
                    EscalatedByUserId INT                 NULL,
                    SlaDueAt          DATETIME2           NOT NULL,
                    CreatedAt         DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
                    UpdatedAt         DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
                    ResolvedAt        DATETIME2           NULL
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_partner_revenue_records_TenantId' AND object_id = OBJECT_ID('dbo.partner_revenue_records'))
                CREATE INDEX IX_partner_revenue_records_TenantId ON dbo.partner_revenue_records(TenantId);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_partner_revenue_records_PartnerTenantId' AND object_id = OBJECT_ID('dbo.partner_revenue_records'))
                CREATE INDEX IX_partner_revenue_records_PartnerTenantId ON dbo.partner_revenue_records(PartnerTenantId);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_partner_support_tickets_TenantId' AND object_id = OBJECT_ID('dbo.partner_support_tickets'))
                CREATE INDEX IX_partner_support_tickets_TenantId ON dbo.partner_support_tickets(TenantId);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_partner_support_tickets_PartnerTenant_Status_Priority' AND object_id = OBJECT_ID('dbo.partner_support_tickets'))
                CREATE INDEX IX_partner_support_tickets_PartnerTenant_Status_Priority
                    ON dbo.partner_support_tickets(PartnerTenantId, Status, Priority);
        ");

        RebuildTenantSecurityPolicy(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'dbo.partner_support_tickets', N'U') IS NOT NULL
                DROP TABLE dbo.partner_support_tickets;

            IF OBJECT_ID(N'dbo.partner_revenue_records', N'U') IS NOT NULL
                DROP TABLE dbo.partner_revenue_records;

            IF OBJECT_ID(N'dbo.partner_configs', N'U') IS NOT NULL
                DROP TABLE dbo.partner_configs;

            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_tenants_ParentTenant')
                ALTER TABLE dbo.tenants DROP CONSTRAINT FK_tenants_ParentTenant;

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tenants_ParentTenantId' AND object_id = OBJECT_ID('dbo.tenants'))
                DROP INDEX IX_tenants_ParentTenantId ON dbo.tenants;

            IF COL_LENGTH('dbo.tenants', 'ParentTenantId') IS NOT NULL
                ALTER TABLE dbo.tenants DROP COLUMN ParentTenantId;
        ");

        RebuildTenantSecurityPolicy(migrationBuilder);
    }

    private static void RebuildTenantSecurityPolicy(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
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
        ");
    }
}
