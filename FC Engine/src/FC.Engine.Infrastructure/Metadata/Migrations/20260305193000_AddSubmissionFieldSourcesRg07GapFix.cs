#nullable enable
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FC.Engine.Infrastructure.Metadata.Migrations;

[DbContext(typeof(MetadataDbContext))]
[Migration("20260305193000_AddSubmissionFieldSourcesRg07GapFix")]
public partial class AddSubmissionFieldSourcesRg07GapFix : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'meta.submission_field_sources', N'U') IS NULL
            BEGIN
                CREATE TABLE meta.submission_field_sources (
                    Id            BIGINT              IDENTITY(1,1) PRIMARY KEY,
                    TenantId      UNIQUEIDENTIFIER    NOT NULL REFERENCES dbo.tenants(TenantId),
                    ReturnCode    NVARCHAR(50)        NOT NULL,
                    SubmissionId  INT                 NOT NULL,
                    FieldName     NVARCHAR(128)       NOT NULL,
                    DataSource    NVARCHAR(30)        NOT NULL,
                    SourceDetail  NVARCHAR(500)       NULL,
                    UpdatedAt     DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT UQ_submission_field_sources_identity
                        UNIQUE (TenantId, ReturnCode, SubmissionId, FieldName)
                );

                CREATE INDEX IX_submission_field_sources_TenantId
                    ON meta.submission_field_sources(TenantId);
                CREATE INDEX IX_submission_field_sources_SubmissionId
                    ON meta.submission_field_sources(SubmissionId);
            END;

            IF OBJECT_ID(N'dbo.TenantSecurityPolicy', N'SP') IS NOT NULL
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.security_predicates sp
                    INNER JOIN sys.tables t ON sp.target_object_id = t.object_id
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'meta'
                      AND t.name = 'submission_field_sources'
                      AND sp.predicate_type_desc = 'FILTER')
                BEGIN
                    EXEC(N'ALTER SECURITY POLICY dbo.TenantSecurityPolicy
                        ADD FILTER PREDICATE dbo.fn_TenantFilter(TenantId) ON meta.submission_field_sources');
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.security_predicates sp
                    INNER JOIN sys.tables t ON sp.target_object_id = t.object_id
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'meta'
                      AND t.name = 'submission_field_sources'
                      AND sp.predicate_type_desc = 'BLOCK')
                BEGIN
                    EXEC(N'ALTER SECURITY POLICY dbo.TenantSecurityPolicy
                        ADD BLOCK PREDICATE dbo.fn_TenantFilter(TenantId) ON meta.submission_field_sources');
                END;
            END;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'dbo.TenantSecurityPolicy', N'SP') IS NOT NULL
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM sys.security_predicates sp
                    INNER JOIN sys.tables t ON sp.target_object_id = t.object_id
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'meta'
                      AND t.name = 'submission_field_sources'
                      AND sp.predicate_type_desc = 'FILTER')
                BEGIN
                    EXEC(N'ALTER SECURITY POLICY dbo.TenantSecurityPolicy
                        DROP FILTER PREDICATE ON meta.submission_field_sources');
                END;

                IF EXISTS (
                    SELECT 1
                    FROM sys.security_predicates sp
                    INNER JOIN sys.tables t ON sp.target_object_id = t.object_id
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'meta'
                      AND t.name = 'submission_field_sources'
                      AND sp.predicate_type_desc = 'BLOCK')
                BEGIN
                    EXEC(N'ALTER SECURITY POLICY dbo.TenantSecurityPolicy
                        DROP BLOCK PREDICATE ON meta.submission_field_sources');
                END;
            END;

            IF OBJECT_ID(N'meta.submission_field_sources', N'U') IS NOT NULL
                DROP TABLE meta.submission_field_sources;
        ");
    }
}
