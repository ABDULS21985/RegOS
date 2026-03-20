using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FC.Engine.Infrastructure.Migrations;

[DbContext(typeof(MetadataDbContext))]
[Migration("20260331120000_AddInstitutionUserPermissionOverrides")]
public partial class AddInstitutionUserPermissionOverrides : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PermissionOverridesJson",
            schema: "meta",
            table: "institution_users",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PermissionOverridesJson",
            schema: "meta",
            table: "institution_users");
    }
}
