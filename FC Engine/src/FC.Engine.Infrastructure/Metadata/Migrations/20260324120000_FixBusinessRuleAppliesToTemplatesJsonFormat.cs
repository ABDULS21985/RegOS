using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FC.Engine.Infrastructure.Metadata.Migrations;

/// <summary>
/// Data-only migration: converts any existing comma-separated AppliesToTemplates values
/// in the business_rules table to proper JSON array format so that MatchesTemplateCode()
/// in FormulaRepository can deserialize them correctly.
///
/// Before: "MFCR 300,MFCR 301"  or  "MFCR 300, BSL 100"
/// After:  ["MFCR 300","MFCR 301"]  or  ["MFCR 300","BSL 100"]
///
/// Values that are already valid JSON arrays (start with '['), NULL, or '*' are left untouched.
/// </summary>
[DbContext(typeof(MetadataDbContext))]
[Migration("20260324120000_FixBusinessRuleAppliesToTemplatesJsonFormat")]
public partial class FixBusinessRuleAppliesToTemplatesJsonFormat : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQL Server: convert comma-separated strings to JSON arrays.
        // Uses STRING_SPLIT + STRING_AGG to rebuild as a JSON array.
        // Skips rows where the value is already JSON (starts with '['), NULL, or '*'.
        migrationBuilder.Sql("""
IF OBJECT_ID('meta.business_rules', 'U') IS NOT NULL
BEGIN
    UPDATE meta.business_rules
    SET AppliesToTemplates = (
        SELECT '[' + STRING_AGG('"' + LTRIM(RTRIM(value)) + '"', ',') + ']'
        FROM STRING_SPLIT(AppliesToTemplates, ',')
    )
    WHERE AppliesToTemplates IS NOT NULL
      AND AppliesToTemplates <> '*'
      AND LEFT(AppliesToTemplates, 1) <> '[';
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverse: convert JSON arrays back to comma-separated.
        // Uses OPENJSON to extract array elements and STRING_AGG to join.
        migrationBuilder.Sql("""
IF OBJECT_ID('meta.business_rules', 'U') IS NOT NULL
BEGIN
    UPDATE meta.business_rules
    SET AppliesToTemplates = (
        SELECT STRING_AGG(j.value, ',')
        FROM OPENJSON(AppliesToTemplates) j
    )
    WHERE AppliesToTemplates IS NOT NULL
      AND AppliesToTemplates <> '*'
      AND LEFT(AppliesToTemplates, 1) = '[';
END
""");
    }
}
