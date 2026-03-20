#nullable enable
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FC.Engine.Infrastructure.Metadata.Migrations;

/// <summary>
/// RG-34: Direct Submission — creates the direct_submissions table for
/// regulator-direct submission via API channel.
/// </summary>
[DbContext(typeof(MetadataDbContext))]
[Migration("20260313232000_AddDirectSubmissionSchemaRg34Legacy")]
public partial class AddDirectSubmissionSchemaRg34Legacy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF OBJECT_ID(N'dbo.direct_submissions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.direct_submissions (
                    Id                      INT             IDENTITY(1,1) PRIMARY KEY,
                    TenantId                UNIQUEIDENTIFIER NOT NULL,
                    SubmissionId            INT             NOT NULL,
                    RegulatorCode           NVARCHAR(20)    NOT NULL,
                    Channel                 NVARCHAR(20)    NOT NULL CONSTRAINT DF_direct_submissions_Channel DEFAULT N'DirectApi',
                    [Status]                NVARCHAR(30)    NOT NULL CONSTRAINT DF_direct_submissions_Status DEFAULT N'Pending',
                    SignatureAlgorithm      NVARCHAR(50)    NULL,
                    SignatureHash           NVARCHAR(128)   NULL,
                    CertificateThumbprint   NVARCHAR(64)    NULL,
                    SignedAt                DATETIME2       NULL,
                    RegulatorReference      NVARCHAR(200)   NULL,
                    RegulatorResponseBody   NVARCHAR(MAX)   NULL,
                    HttpStatusCode          INT             NULL,
                    AttemptCount            INT             NOT NULL CONSTRAINT DF_direct_submissions_AttemptCount DEFAULT 0,
                    MaxAttempts             INT             NOT NULL CONSTRAINT DF_direct_submissions_MaxAttempts DEFAULT 3,
                    NextRetryAt             DATETIME2       NULL,
                    LastAttemptAt           DATETIME2       NULL,
                    SubmittedAt             DATETIME2       NULL,
                    AcknowledgedAt          DATETIME2       NULL,
                    ErrorMessage            NVARCHAR(MAX)   NULL,
                    PackageStoragePath      NVARCHAR(500)   NULL,
                    PackageSizeBytes        BIGINT          NULL,
                    PackageSha256           NVARCHAR(64)    NULL,
                    CreatedAt               DATETIME2       NOT NULL CONSTRAINT DF_direct_submissions_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CreatedBy               NVARCHAR(100)   NOT NULL,

                    CONSTRAINT FK_direct_submissions_return_submissions
                        FOREIGN KEY (SubmissionId) REFERENCES dbo.return_submissions(Id) ON DELETE CASCADE
                );

                CREATE NONCLUSTERED INDEX IX_direct_submissions_tenant
                    ON dbo.direct_submissions (TenantId);

                CREATE NONCLUSTERED INDEX IX_direct_submissions_tenant_submission
                    ON dbo.direct_submissions (TenantId, SubmissionId);

                CREATE NONCLUSTERED INDEX IX_direct_submissions_status_next_retry
                    ON dbo.direct_submissions ([Status], NextRetryAt);

                CREATE NONCLUSTERED INDEX IX_direct_submissions_regulator_reference
                    ON dbo.direct_submissions (RegulatorReference);

                CREATE NONCLUSTERED INDEX IX_direct_submissions_submission
                    ON dbo.direct_submissions (SubmissionId);
            END
        ");

        // Add to RLS policy
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

            IF OBJECT_ID(N'dbo.direct_submissions', N'U') IS NOT NULL
                DROP TABLE dbo.direct_submissions;

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
