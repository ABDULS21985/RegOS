using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FC.Engine.Infrastructure.Migrations;

[DbContext(typeof(MetadataDbContext))]
[Migration("20260404000000_RepairStressScenarioRegulatorCode")]
/// <summary>
/// Repairs stress scenario regulator scoping for databases where
/// AddRegulatorCodeToStressScenarios ran before StressScenarios existed.
/// </summary>
public partial class RepairStressScenarioRegulatorCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID('StressScenarios', 'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM   sys.columns c
    JOIN   sys.tables  t ON t.object_id = c.object_id
    WHERE  t.name = 'StressScenarios'
      AND  c.name = 'RegulatorCode'
)
BEGIN
    ALTER TABLE StressScenarios
        ADD RegulatorCode VARCHAR(10) NULL;
END
""");

        migrationBuilder.Sql("""
IF OBJECT_ID('StressScenarios', 'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM   sys.indexes
    WHERE  name      = 'IX_StressScenarios_RegulatorCode'
      AND  object_id = OBJECT_ID('StressScenarios')
)
BEGIN
    CREATE INDEX IX_StressScenarios_RegulatorCode
        ON StressScenarios (RegulatorCode, IsActive, Category);
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1
    FROM   sys.indexes
    WHERE  name      = 'IX_StressScenarios_RegulatorCode'
      AND  object_id = OBJECT_ID('StressScenarios')
)
BEGIN
    DROP INDEX IX_StressScenarios_RegulatorCode ON StressScenarios;
END
""");

        migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1
    FROM   sys.columns c
    JOIN   sys.tables  t ON t.object_id = c.object_id
    WHERE  t.name = 'StressScenarios'
      AND  c.name = 'RegulatorCode'
)
BEGIN
    ALTER TABLE StressScenarios
        DROP COLUMN RegulatorCode;
END
""");
    }
}
