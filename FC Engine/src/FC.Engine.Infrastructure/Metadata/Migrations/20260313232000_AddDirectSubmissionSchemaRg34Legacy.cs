using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FC.Engine.Infrastructure.Metadata.Migrations;

[DbContext(typeof(MetadataDbContext))]
[Migration("20260313232000_AddDirectSubmissionSchemaRg34Legacy")]
public partial class AddDirectSubmissionSchemaRg34Legacy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "direct_submissions",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubmissionId = table.Column<int>(type: "int", nullable: false),
                RegulatorCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "DirectApi"),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                SignatureAlgorithm = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                SignatureHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                CertificateThumbprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                SignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RegulatorReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                RegulatorResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                MaxAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                NextRetryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PackageStoragePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                PackageSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                PackageSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_direct_submissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_direct_submissions_return_submissions",
                    column: x => x.SubmissionId,
                    principalTable: "return_submissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_direct_submissions_tenant",
            table: "direct_submissions",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_direct_submissions_tenant_submission",
            table: "direct_submissions",
            columns: new[] { "TenantId", "SubmissionId" });

        migrationBuilder.CreateIndex(
            name: "IX_direct_submissions_status_next_retry",
            table: "direct_submissions",
            columns: new[] { "Status", "NextRetryAt" });

        migrationBuilder.CreateIndex(
            name: "IX_direct_submissions_regulator_reference",
            table: "direct_submissions",
            column: "RegulatorReference");

        migrationBuilder.CreateIndex(
            name: "IX_direct_submissions_submission",
            table: "direct_submissions",
            column: "SubmissionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "direct_submissions");
    }
}
