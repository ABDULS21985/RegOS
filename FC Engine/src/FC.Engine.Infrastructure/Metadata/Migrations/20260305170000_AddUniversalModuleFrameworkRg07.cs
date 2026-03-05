#nullable enable
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FC.Engine.Infrastructure.Metadata.Migrations;

[DbContext(typeof(MetadataDbContext))]
[Migration("20260305170000_AddUniversalModuleFrameworkRg07")]
public partial class AddUniversalModuleFrameworkRg07 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            -- Ensure legacy/global templates are linked to FC_RETURNS module for backward compatibility.
            DECLARE @fcModuleId INT = (SELECT TOP 1 Id FROM dbo.modules WHERE ModuleCode = 'FC_RETURNS');
            IF @fcModuleId IS NOT NULL
            BEGIN
                UPDATE meta.return_templates
                SET ModuleId = @fcModuleId
                WHERE ModuleId IS NULL;
            END;

            IF OBJECT_ID(N'dbo.module_versions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.module_versions (
                    Id              INT             IDENTITY(1,1) PRIMARY KEY,
                    ModuleId        INT             NOT NULL REFERENCES dbo.modules(Id),
                    VersionCode     NVARCHAR(20)    NOT NULL,
                    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Draft',
                    PublishedAt     DATETIME2       NULL,
                    DeprecatedAt    DATETIME2       NULL,
                    ReleaseNotes    NVARCHAR(MAX)   NULL,
                    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE INDEX UX_module_versions_ModuleId_VersionCode
                    ON dbo.module_versions(ModuleId, VersionCode);
                CREATE INDEX IX_module_versions_ModuleId
                    ON dbo.module_versions(ModuleId);
            END;

            IF OBJECT_ID(N'dbo.inter_module_data_flows', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.inter_module_data_flows (
                    Id                      INT             IDENTITY(1,1) PRIMARY KEY,
                    SourceModuleId          INT             NOT NULL REFERENCES dbo.modules(Id),
                    SourceTemplateCode      NVARCHAR(50)    NOT NULL,
                    SourceFieldCode         NVARCHAR(50)    NOT NULL,
                    TargetModuleCode        NVARCHAR(30)    NOT NULL,
                    TargetTemplateCode      NVARCHAR(50)    NOT NULL,
                    TargetFieldCode         NVARCHAR(50)    NOT NULL,
                    TransformationType      NVARCHAR(20)    NOT NULL DEFAULT 'DirectCopy',
                    TransformFormula        NVARCHAR(500)   NULL,
                    Description             NVARCHAR(500)   NULL,
                    IsActive                BIT             NOT NULL DEFAULT 1,
                    CreatedAt               DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_inter_module_data_flows_TargetModuleCode
                        FOREIGN KEY (TargetModuleCode) REFERENCES dbo.modules(ModuleCode)
                );

                CREATE UNIQUE INDEX UX_inter_module_data_flows_identity
                    ON dbo.inter_module_data_flows(
                        SourceModuleId,
                        SourceTemplateCode,
                        SourceFieldCode,
                        TargetModuleCode,
                        TargetTemplateCode,
                        TargetFieldCode);
                CREATE INDEX IX_inter_module_data_flows_SourceModuleId
                    ON dbo.inter_module_data_flows(SourceModuleId);
                CREATE INDEX IX_inter_module_data_flows_TargetModuleCode
                    ON dbo.inter_module_data_flows(TargetModuleCode);
            END;

            IF COL_LENGTH('meta.cross_sheet_rules', 'ModuleId') IS NULL
            BEGIN
                ALTER TABLE meta.cross_sheet_rules ADD ModuleId INT NULL;
                ALTER TABLE meta.cross_sheet_rules WITH CHECK
                    ADD CONSTRAINT FK_cross_sheet_rules_ModuleId
                    FOREIGN KEY (ModuleId) REFERENCES dbo.modules(Id);
            END;

            IF COL_LENGTH('meta.cross_sheet_rules', 'SourceModuleId') IS NULL
            BEGIN
                ALTER TABLE meta.cross_sheet_rules ADD SourceModuleId INT NULL;
                ALTER TABLE meta.cross_sheet_rules WITH CHECK
                    ADD CONSTRAINT FK_cross_sheet_rules_SourceModuleId
                    FOREIGN KEY (SourceModuleId) REFERENCES dbo.modules(Id);
            END;

            IF COL_LENGTH('meta.cross_sheet_rules', 'TargetModuleId') IS NULL
            BEGIN
                ALTER TABLE meta.cross_sheet_rules ADD TargetModuleId INT NULL;
                ALTER TABLE meta.cross_sheet_rules WITH CHECK
                    ADD CONSTRAINT FK_cross_sheet_rules_TargetModuleId
                    FOREIGN KEY (TargetModuleId) REFERENCES dbo.modules(Id);
            END;

            IF COL_LENGTH('meta.cross_sheet_rules', 'SourceTemplateCode') IS NULL
                ALTER TABLE meta.cross_sheet_rules ADD SourceTemplateCode NVARCHAR(50) NULL;

            IF COL_LENGTH('meta.cross_sheet_rules', 'SourceFieldCode') IS NULL
                ALTER TABLE meta.cross_sheet_rules ADD SourceFieldCode NVARCHAR(50) NULL;

            IF COL_LENGTH('meta.cross_sheet_rules', 'TargetTemplateCode') IS NULL
                ALTER TABLE meta.cross_sheet_rules ADD TargetTemplateCode NVARCHAR(50) NULL;

            IF COL_LENGTH('meta.cross_sheet_rules', 'TargetFieldCode') IS NULL
                ALTER TABLE meta.cross_sheet_rules ADD TargetFieldCode NVARCHAR(50) NULL;

            IF COL_LENGTH('meta.cross_sheet_rules', 'Operator') IS NULL
                ALTER TABLE meta.cross_sheet_rules ADD [Operator] NVARCHAR(30) NULL;

            IF COL_LENGTH('meta.cross_sheet_rules', 'ToleranceAmount') IS NULL
                ALTER TABLE meta.cross_sheet_rules ADD ToleranceAmount DECIMAL(20,2) NOT NULL CONSTRAINT DF_cross_sheet_rules_ToleranceAmount DEFAULT 0;

            IF COL_LENGTH('meta.cross_sheet_rules', 'TolerancePercent') IS NULL
                ALTER TABLE meta.cross_sheet_rules ADD TolerancePercent DECIMAL(10,4) NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cross_sheet_rules_ModuleId' AND object_id = OBJECT_ID('meta.cross_sheet_rules'))
                CREATE INDEX IX_cross_sheet_rules_ModuleId ON meta.cross_sheet_rules(ModuleId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cross_sheet_rules_SourceModuleId' AND object_id = OBJECT_ID('meta.cross_sheet_rules'))
                CREATE INDEX IX_cross_sheet_rules_SourceModuleId ON meta.cross_sheet_rules(SourceModuleId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cross_sheet_rules_TargetModuleId' AND object_id = OBJECT_ID('meta.cross_sheet_rules'))
                CREATE INDEX IX_cross_sheet_rules_TargetModuleId ON meta.cross_sheet_rules(TargetModuleId);
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'dbo.inter_module_data_flows', N'U') IS NOT NULL
                DROP TABLE dbo.inter_module_data_flows;

            IF OBJECT_ID(N'dbo.module_versions', N'U') IS NOT NULL
                DROP TABLE dbo.module_versions;

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cross_sheet_rules_TargetModuleId' AND object_id = OBJECT_ID('meta.cross_sheet_rules'))
                DROP INDEX IX_cross_sheet_rules_TargetModuleId ON meta.cross_sheet_rules;
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cross_sheet_rules_SourceModuleId' AND object_id = OBJECT_ID('meta.cross_sheet_rules'))
                DROP INDEX IX_cross_sheet_rules_SourceModuleId ON meta.cross_sheet_rules;
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cross_sheet_rules_ModuleId' AND object_id = OBJECT_ID('meta.cross_sheet_rules'))
                DROP INDEX IX_cross_sheet_rules_ModuleId ON meta.cross_sheet_rules;

            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cross_sheet_rules_TargetModuleId')
                ALTER TABLE meta.cross_sheet_rules DROP CONSTRAINT FK_cross_sheet_rules_TargetModuleId;
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cross_sheet_rules_SourceModuleId')
                ALTER TABLE meta.cross_sheet_rules DROP CONSTRAINT FK_cross_sheet_rules_SourceModuleId;
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_cross_sheet_rules_ModuleId')
                ALTER TABLE meta.cross_sheet_rules DROP CONSTRAINT FK_cross_sheet_rules_ModuleId;

            IF COL_LENGTH('meta.cross_sheet_rules', 'TolerancePercent') IS NOT NULL
                ALTER TABLE meta.cross_sheet_rules DROP COLUMN TolerancePercent;
            IF COL_LENGTH('meta.cross_sheet_rules', 'ToleranceAmount') IS NOT NULL
                ALTER TABLE meta.cross_sheet_rules DROP COLUMN ToleranceAmount;
            IF COL_LENGTH('meta.cross_sheet_rules', 'Operator') IS NOT NULL
                ALTER TABLE meta.cross_sheet_rules DROP COLUMN [Operator];
            IF COL_LENGTH('meta.cross_sheet_rules', 'TargetFieldCode') IS NOT NULL
                ALTER TABLE meta.cross_sheet_rules DROP COLUMN TargetFieldCode;
            IF COL_LENGTH('meta.cross_sheet_rules', 'TargetTemplateCode') IS NOT NULL
                ALTER TABLE meta.cross_sheet_rules DROP COLUMN TargetTemplateCode;
            IF COL_LENGTH('meta.cross_sheet_rules', 'SourceFieldCode') IS NOT NULL
                ALTER TABLE meta.cross_sheet_rules DROP COLUMN SourceFieldCode;
            IF COL_LENGTH('meta.cross_sheet_rules', 'SourceTemplateCode') IS NOT NULL
                ALTER TABLE meta.cross_sheet_rules DROP COLUMN SourceTemplateCode;
            IF COL_LENGTH('meta.cross_sheet_rules', 'TargetModuleId') IS NOT NULL
                ALTER TABLE meta.cross_sheet_rules DROP COLUMN TargetModuleId;
            IF COL_LENGTH('meta.cross_sheet_rules', 'SourceModuleId') IS NOT NULL
                ALTER TABLE meta.cross_sheet_rules DROP COLUMN SourceModuleId;
            IF COL_LENGTH('meta.cross_sheet_rules', 'ModuleId') IS NOT NULL
                ALTER TABLE meta.cross_sheet_rules DROP COLUMN ModuleId;
        ");
    }
}
