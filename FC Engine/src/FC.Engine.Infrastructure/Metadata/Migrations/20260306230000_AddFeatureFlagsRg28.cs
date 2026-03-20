#nullable enable
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FC.Engine.Infrastructure.Metadata.Migrations;

[DbContext(typeof(MetadataDbContext))]
[Migration("20260306230000_AddFeatureFlagsRg28")]
public partial class AddFeatureFlagsRg28 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.feature_flags', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.feature_flags (
                    Id              INT             IDENTITY(1,1) PRIMARY KEY,
                    FlagCode        NVARCHAR(50)    NOT NULL,
                    Description     NVARCHAR(200)   NOT NULL,
                    IsEnabled       BIT             NOT NULL DEFAULT 0,
                    RolloutPercent  INT             NOT NULL DEFAULT 0,
                    AllowedTenants  NVARCHAR(MAX)   NULL,
                    AllowedPlans    NVARCHAR(MAX)   NULL,
                    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
                    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT UQ_feature_flags_FlagCode UNIQUE (FlagCode)
                );
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.feature_flags', N'U') IS NOT NULL
                DROP TABLE dbo.feature_flags;
            """);
    }
}
