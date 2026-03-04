#nullable enable
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FC.Engine.Infrastructure.Metadata.Migrations;

[DbContext(typeof(MetadataDbContext))]
[Migration("20260304300000_AddSubscriptionPlansAndOnboardingEnhancements")]
public partial class AddSubscriptionPlansAndOnboardingEnhancements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ═══════════════════════════════════════════════════════════
        // Step 1: Create subscription_plans table
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            CREATE TABLE dbo.subscription_plans (
                Id                  INT             IDENTITY(1,1) PRIMARY KEY,
                PlanCode            NVARCHAR(30)    NOT NULL,
                PlanName            NVARCHAR(200)   NOT NULL,
                Description         NVARCHAR(1000)  NULL,
                MaxInstitutions     INT             NOT NULL DEFAULT 1,
                MaxUsersPerEntity   INT             NOT NULL DEFAULT 10,
                MaxModules          INT             NOT NULL DEFAULT 1,
                AllModulesIncluded  BIT             NOT NULL DEFAULT 0,
                Features            NVARCHAR(500)   NOT NULL DEFAULT 'xml_submission,validation,reporting',
                IsActive            BIT             NOT NULL DEFAULT 1,
                DisplayOrder        INT             NOT NULL DEFAULT 0,
                CreatedAt           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
            );
            CREATE UNIQUE INDEX IX_subscription_plans_PlanCode ON dbo.subscription_plans(PlanCode);
        ");

        // ═══════════════════════════════════════════════════════════
        // Step 2: Seed default subscription plans
        // ═══════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            INSERT INTO dbo.subscription_plans
                (PlanCode, PlanName, Description, MaxInstitutions, MaxUsersPerEntity, MaxModules, AllModulesIncluded, Features, DisplayOrder)
            VALUES
            (
                'STARTER',
                'Starter',
                'Basic plan for small institutions with a single regulatory module. Ideal for finance companies, BDCs, and other single-licence entities.',
                1,
                10,
                1,
                0,
                'xml_submission,validation,reporting',
                1
            ),
            (
                'PROFESSIONAL',
                'Professional',
                'Mid-tier plan for institutions that need multiple regulatory modules, API access, and advanced reporting capabilities.',
                3,
                25,
                5,
                0,
                'xml_submission,validation,reporting,api_access,bulk_upload,advanced_reporting',
                2
            ),
            (
                'ENTERPRISE',
                'Enterprise',
                'Full-featured plan for large institutions requiring all modules, unlimited users, and premium support including white-label capabilities.',
                10,
                100,
                999,
                1,
                'xml_submission,validation,reporting,api_access,bulk_upload,advanced_reporting,white_label,priority_support,custom_branding',
                3
            ),
            (
                'GROUP',
                'Group',
                'Holding group plan for conglomerates managing multiple subsidiaries across different licence categories with consolidated reporting.',
                50,
                200,
                999,
                1,
                'xml_submission,validation,reporting,api_access,bulk_upload,advanced_reporting,white_label,priority_support,custom_branding,consolidated_reporting,subsidiary_management',
                4
            );
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"DROP TABLE IF EXISTS dbo.subscription_plans;");
    }
}
