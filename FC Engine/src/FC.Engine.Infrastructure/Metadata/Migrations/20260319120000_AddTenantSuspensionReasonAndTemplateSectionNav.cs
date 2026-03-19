using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FC.Engine.Infrastructure.Metadata.Migrations;

/// <inheritdoc />
public partial class AddTenantSuspensionReasonAndTemplateSectionNav : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add SuspensionReason column to Tenants table
        migrationBuilder.AddColumn<string>(
            name: "SuspensionReason",
            schema: "dbo",
            table: "tenants",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SuspensionReason",
            schema: "dbo",
            table: "tenants");
    }
}
