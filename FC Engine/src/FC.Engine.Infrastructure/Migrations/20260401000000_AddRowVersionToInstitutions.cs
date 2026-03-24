using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FC.Engine.Infrastructure.Migrations;

[DbContext(typeof(MetadataDbContext))]
[Migration("20260401000000_AddRowVersionToInstitutions")]
public partial class AddRowVersionToInstitutions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns c
    JOIN   sys.tables  t ON t.object_id = c.object_id
    WHERE  t.name = 'Institutions'
      AND  c.name = 'RowVersion'
)
BEGIN
    ALTER TABLE Institutions
        ADD RowVersion ROWVERSION NULL;
END
""");

        // Tenant.SuspensionReason — added to domain model but no migration existed
        migrationBuilder.Sql("""
IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns c
    JOIN   sys.tables  t ON t.object_id = c.object_id
    WHERE  t.name = 'Tenants'
      AND  c.name = 'SuspensionReason'
)
BEGIN
    ALTER TABLE Tenants
        ADD SuspensionReason NVARCHAR(500) NULL;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1
    FROM   sys.columns c
    JOIN   sys.tables  t ON t.object_id = c.object_id
    WHERE  t.name = 'Institutions' AND c.name = 'RowVersion'
)
BEGIN
    ALTER TABLE Institutions DROP COLUMN RowVersion;
END
""");

        migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1
    FROM   sys.columns c
    JOIN   sys.tables  t ON t.object_id = c.object_id
    WHERE  t.name = 'Tenants' AND c.name = 'SuspensionReason'
)
BEGIN
    ALTER TABLE Tenants DROP COLUMN SuspensionReason;
END
""");
    }
}
