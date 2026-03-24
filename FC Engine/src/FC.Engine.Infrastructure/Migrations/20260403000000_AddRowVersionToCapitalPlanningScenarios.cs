using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FC.Engine.Infrastructure.Migrations;

[DbContext(typeof(MetadataDbContext))]
[Migration("20260403000000_AddRowVersionToCapitalPlanningScenarios")]
public partial class AddRowVersionToCapitalPlanningScenarios : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns c
    JOIN   sys.tables  t ON t.object_id = c.object_id
    JOIN   sys.schemas s ON s.schema_id = t.schema_id
    WHERE  s.name = 'meta'
      AND  t.name = 'capital_planning_scenarios'
      AND  c.name = 'RowVersion'
)
BEGIN
    ALTER TABLE meta.capital_planning_scenarios
        ADD RowVersion ROWVERSION NULL;
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
    JOIN   sys.schemas s ON s.schema_id = t.schema_id
    WHERE  s.name = 'meta'
      AND  t.name = 'capital_planning_scenarios'
      AND  c.name = 'RowVersion'
)
BEGIN
    ALTER TABLE meta.capital_planning_scenarios DROP COLUMN RowVersion;
END
""");
    }
}
