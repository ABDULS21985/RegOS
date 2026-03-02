I have now thoroughly explored every file in the codebase. I have a complete understanding of:

1. The 103 data tables in `schema.sql` and their structural categories (FixedRow, MultiRow, ItemCoded)
2. The existing code-driven architecture: per-template C# classes, XSD files, XML parsers, EF configurations, validation rules
3. The current submission pipeline: API endpoint -> IngestionOrchestrator -> XSD validation -> XML parsing -> Validation engine -> Persistence
4. All domain entities, value objects, enums, and interfaces

Let me now produce the comprehensive metadata-driven architecture design.

---

# CBN DFIS FC Returns Data Processing Engine -- Metadata-Driven Architecture

## Preamble: Analysis of What Exists and What Must Be Replaced

Having examined every file in the repository, the current code-driven architecture requires the following per-template artifacts for each of the 103 return templates:

- **One C# domain class** (e.g., `/Users/mac/codes/fcs/FC Engine/src/FC.Engine.Domain/Returns/FixedRow/Mfcr300Data.cs` -- 104 lines mapping ~40 `[LineCode]` properties)
- **One XSD schema file** (e.g., `/Users/mac/codes/fcs/FC Engine/src/FC.Engine.Infrastructure/Xml/Schemas/MFCR300.xsd` -- 115 lines, manually authored)
- **One XML parser class** (e.g., `/Users/mac/codes/fcs/FC Engine/src/FC.Engine.Infrastructure/Xml/Parsers/Mfcr300XmlParser.cs` -- 97 lines, hand-coded property-by-property mapping)
- **One EF entity class** (e.g., `/Users/mac/codes/fcs/FC Engine/src/FC.Engine.Infrastructure/Persistence/Entities/Mfcr300Entity.cs` -- 103 lines)
- **One EF configuration class** (e.g., `/Users/mac/codes/fcs/FC Engine/src/FC.Engine.Infrastructure/Persistence/Configurations/Mfcr300Configuration.cs` -- 101 lines)
- **One validation rule class** (e.g., `/Users/mac/codes/fcs/FC Engine/src/FC.Engine.Infrastructure/Validation/Rules/IntraSheet/Mfcr300SumRules.cs` -- 144 lines)
- **One section in ReturnRepository.cs** (`switch` branch per template type)
- **One DI registration line** in `/Users/mac/codes/fcs/FC Engine/src/FC.Engine.Infrastructure/DependencyInjection.cs`

Completing all 103 templates this way would produce approximately **70,000+ lines of highly repetitive C# code** plus 103 XSD files. The metadata-driven approach replaces all of this with database-driven configuration interpreted at runtime.

---

## A. Database Schema for Metadata

### A.1 Core Metadata Tables (the "Template Registry")

```sql
-- ============================================================================
-- METADATA SCHEMA: Template Registry
-- These tables define the structure of ALL return templates.
-- CBN admins manage these through the Admin Portal.
-- ============================================================================

-- Template lifecycle: Draft -> Review -> Published -> Deprecated -> Retired
CREATE TABLE meta.template_statuses (
    id          INT IDENTITY(1,1) PRIMARY KEY,
    name        VARCHAR(20) NOT NULL UNIQUE,  -- Draft, Review, Published, Deprecated, Retired
    description VARCHAR(200)
);

-- Master table: one row per return template
CREATE TABLE meta.return_templates (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    return_code         VARCHAR(20) NOT NULL UNIQUE,     -- 'MFCR 300', 'QFCR 364', etc.
    name                NVARCHAR(255) NOT NULL,           -- 'Statement of Financial Position'
    description         NVARCHAR(1000),
    frequency           VARCHAR(20) NOT NULL,             -- Monthly, Quarterly, SemiAnnual, Computed
    structural_category VARCHAR(20) NOT NULL,             -- FixedRow, MultiRow, ItemCoded
    physical_table_name VARCHAR(128) NOT NULL UNIQUE,     -- 'mfcr_300', 'qfcr_364'
    xml_root_element    VARCHAR(128) NOT NULL,            -- 'MFCR300', 'QFCR364'
    xml_namespace       VARCHAR(255) NOT NULL,            -- 'urn:cbn:dfis:fc:mfcr300'
    is_system_template  BIT NOT NULL DEFAULT 0,           -- 1 = seeded from schema.sql, 0 = user-created
    owner_department    VARCHAR(50) DEFAULT 'DFIS',
    institution_type    VARCHAR(10) DEFAULT 'FC',
    created_at          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    created_by          NVARCHAR(100) NOT NULL,
    updated_at          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_by          NVARCHAR(100) NOT NULL
);

-- Version tracking: every published change creates a new version
CREATE TABLE meta.template_versions (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    template_id         INT NOT NULL REFERENCES meta.return_templates(id),
    version_number      INT NOT NULL,                     -- 1, 2, 3...
    status_id           INT NOT NULL REFERENCES meta.template_statuses(id),
    effective_from      DATE,                             -- when Published: first period this version applies
    effective_to        DATE,                             -- NULL = current; set when deprecated
    change_summary      NVARCHAR(1000),
    approved_by         NVARCHAR(100),
    approved_at         DATETIME2,
    published_at        DATETIME2,
    ddl_script          NVARCHAR(MAX),                    -- the CREATE/ALTER TABLE DDL that was executed
    rollback_script     NVARCHAR(MAX),                    -- inverse DDL for rollback
    created_at          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    created_by          NVARCHAR(100) NOT NULL,
    CONSTRAINT UQ_template_version UNIQUE (template_id, version_number)
);

-- Field definitions: one row per column/field in a template
CREATE TABLE meta.template_fields (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    template_version_id INT NOT NULL REFERENCES meta.template_versions(id),
    field_name          VARCHAR(128) NOT NULL,             -- SQL column name: 'cash_notes'
    display_name        NVARCHAR(255) NOT NULL,            -- UI label: 'Cash - Notes'
    xml_element_name    VARCHAR(128) NOT NULL,             -- XML element: 'CashNotes'
    line_code           VARCHAR(20),                       -- CBN line code: '10110'
    section_name        NVARCHAR(100),                     -- grouping: 'Financial Assets: Cash'
    section_order       INT NOT NULL DEFAULT 0,            -- order within section
    field_order         INT NOT NULL DEFAULT 0,            -- order within template
    data_type           VARCHAR(30) NOT NULL,              -- Money, Integer, Decimal, Text, Date, Boolean, Percentage
    sql_type            VARCHAR(50) NOT NULL,              -- 'DECIMAL(20,2)', 'VARCHAR(255)', 'INT', 'DATE', 'BIT'
    is_required         BIT NOT NULL DEFAULT 0,
    is_computed         BIT NOT NULL DEFAULT 0,            -- true for total/subtotal fields
    is_key_field        BIT NOT NULL DEFAULT 0,            -- true for serial_no, item_code (row identity)
    default_value       NVARCHAR(100),
    min_value           NVARCHAR(100),                     -- for range validation
    max_value           NVARCHAR(100),
    max_length          INT,                               -- for text fields
    allowed_values      NVARCHAR(MAX),                     -- JSON array: ["Secured","Unsecured"]
    reference_table     VARCHAR(128),                      -- FK to e.g. 'bank_codes', 'sectors'
    reference_column    VARCHAR(128),
    help_text           NVARCHAR(500),
    is_ytd_field        BIT NOT NULL DEFAULT 0,            -- for YTD companion fields
    ytd_source_field_id INT REFERENCES meta.template_fields(id), -- links YTD field to its source
    created_at          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_field_per_version UNIQUE (template_version_id, field_name)
);

-- Predefined item codes for ItemCoded templates (e.g., MFCR 356 item_code: 33002, 33008...)
CREATE TABLE meta.template_item_codes (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    template_version_id INT NOT NULL REFERENCES meta.template_versions(id),
    item_code           VARCHAR(20) NOT NULL,
    item_description    NVARCHAR(255) NOT NULL,
    sort_order          INT NOT NULL DEFAULT 0,
    is_total_row        BIT NOT NULL DEFAULT 0,            -- this row is a computed total
    created_at          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_item_code_per_version UNIQUE (template_version_id, item_code)
);

-- Template sections for UI grouping
CREATE TABLE meta.template_sections (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    template_version_id INT NOT NULL REFERENCES meta.template_versions(id),
    section_name        NVARCHAR(100) NOT NULL,
    section_order       INT NOT NULL DEFAULT 0,
    description         NVARCHAR(500),
    is_repeating        BIT NOT NULL DEFAULT 0,            -- for MultiRow sections
    CONSTRAINT UQ_section_per_version UNIQUE (template_version_id, section_name)
);
```

### A.2 Validation Rule Metadata Tables

```sql
-- ============================================================================
-- VALIDATION METADATA: Formulas and Rules
-- ============================================================================

-- Intra-sheet formulas: single-template field-level validation
CREATE TABLE meta.intra_sheet_formulas (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    template_version_id INT NOT NULL REFERENCES meta.template_versions(id),
    rule_code           VARCHAR(50) NOT NULL,              -- 'MFCR300_SUM_TOTAL_CASH'
    rule_name           NVARCHAR(255) NOT NULL,            -- 'Total Cash = Cash Notes + Cash Coins'
    formula_type        VARCHAR(30) NOT NULL,              -- Sum, Difference, Equals, GreaterThan, LessThan,
                                                           -- GreaterThanOrEqual, Between, Ratio, Custom
    target_field_name   VARCHAR(128) NOT NULL,             -- 'total_cash' (the computed field)
    target_line_code    VARCHAR(20),                       -- '10140'
    operand_fields      NVARCHAR(MAX) NOT NULL,            -- JSON: ["cash_notes","cash_coins"]
    operand_line_codes  NVARCHAR(MAX),                     -- JSON: ["10110","10120"]
    custom_expression   NVARCHAR(1000),                    -- for Custom type: "A + B - C" using field aliases
    tolerance_amount    DECIMAL(20,2) DEFAULT 0,           -- allow rounding: 0.01
    tolerance_percent   DECIMAL(10,4),                     -- or percentage tolerance
    severity            VARCHAR(10) NOT NULL DEFAULT 'Error', -- Error, Warning, Info
    error_message       NVARCHAR(500),                     -- custom message; NULL = auto-generated
    is_active           BIT NOT NULL DEFAULT 1,
    sort_order          INT NOT NULL DEFAULT 0,
    created_at          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    created_by          NVARCHAR(100) NOT NULL,
    CONSTRAINT UQ_intra_rule UNIQUE (template_version_id, rule_code)
);

-- Cross-sheet rules: validation spanning two or more templates
CREATE TABLE meta.cross_sheet_rules (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    rule_code           VARCHAR(50) NOT NULL UNIQUE,       -- 'XS-001'
    rule_name           NVARCHAR(255) NOT NULL,
    description         NVARCHAR(1000),
    severity            VARCHAR(10) NOT NULL DEFAULT 'Error',
    is_active           BIT NOT NULL DEFAULT 1,
    created_at          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    created_by          NVARCHAR(100) NOT NULL
);

-- Cross-sheet rule operands: identifies which templates and fields participate
CREATE TABLE meta.cross_sheet_rule_operands (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    rule_id             INT NOT NULL REFERENCES meta.cross_sheet_rules(id),
    operand_alias       VARCHAR(10) NOT NULL,              -- 'A', 'B', 'C'
    template_return_code VARCHAR(20) NOT NULL,             -- 'MFCR 300'
    field_name          VARCHAR(128) NOT NULL,             -- 'total_assets'
    line_code           VARCHAR(20),
    -- For MultiRow/ItemCoded: aggregate function to apply
    aggregate_function  VARCHAR(20),                       -- NULL (FixedRow), SUM, COUNT, MAX, MIN, AVG
    filter_item_code    VARCHAR(20),                       -- for ItemCoded: specific item_code row
    sort_order          INT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_cross_operand UNIQUE (rule_id, operand_alias)
);

-- Cross-sheet rule expression: the formula using operand aliases
CREATE TABLE meta.cross_sheet_rule_expressions (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    rule_id             INT NOT NULL REFERENCES meta.cross_sheet_rules(id) UNIQUE,
    expression          NVARCHAR(1000) NOT NULL,           -- 'A = B + C' or 'A >= B * 0.125'
    tolerance_amount    DECIMAL(20,2) DEFAULT 0,
    tolerance_percent   DECIMAL(10,4),
    error_message       NVARCHAR(500)
);

-- Business rules: general-purpose rules (deadline checks, threshold checks, etc.)
CREATE TABLE meta.business_rules (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    rule_code           VARCHAR(50) NOT NULL UNIQUE,
    rule_name           NVARCHAR(255) NOT NULL,
    description         NVARCHAR(1000),
    rule_type           VARCHAR(30) NOT NULL,              -- DateCheck, ThresholdCheck, Completeness, Custom
    expression          NVARCHAR(1000),                    -- e.g., 'capital_adequacy_ratio >= 0.125'
    applies_to_templates NVARCHAR(MAX),                    -- JSON: ["MFCR 300","FC CAR 2"] or "*" for all
    applies_to_fields   NVARCHAR(MAX),                     -- JSON: ["capital_adequacy_ratio"] or NULL for template-level
    severity            VARCHAR(10) NOT NULL DEFAULT 'Error',
    is_active           BIT NOT NULL DEFAULT 1,
    created_at          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    created_by          NVARCHAR(100) NOT NULL
);
```

### A.3 Audit and Migration Tracking Tables

```sql
-- ============================================================================
-- AUDIT AND MIGRATION TRACKING
-- ============================================================================

-- Audit trail: every change to metadata
CREATE TABLE meta.audit_log (
    id                  BIGINT IDENTITY(1,1) PRIMARY KEY,
    entity_type         VARCHAR(50) NOT NULL,              -- 'ReturnTemplate', 'TemplateField', 'IntraSheetFormula', etc.
    entity_id           INT NOT NULL,
    action              VARCHAR(20) NOT NULL,              -- 'Create', 'Update', 'Delete', 'Publish', 'Deprecate'
    old_values          NVARCHAR(MAX),                     -- JSON of previous state
    new_values          NVARCHAR(MAX),                     -- JSON of new state
    performed_by        NVARCHAR(100) NOT NULL,
    performed_at        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ip_address          VARCHAR(45),
    correlation_id      UNIQUEIDENTIFIER
);

-- DDL migration history: tracks every schema change to physical data tables
CREATE TABLE meta.ddl_migrations (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    template_id         INT NOT NULL REFERENCES meta.return_templates(id),
    version_from        INT,                               -- NULL for initial creation
    version_to          INT NOT NULL,
    migration_type      VARCHAR(20) NOT NULL,              -- 'CreateTable', 'AddColumn', 'AlterColumn', 'DropColumn'
    ddl_script          NVARCHAR(MAX) NOT NULL,
    rollback_script     NVARCHAR(MAX) NOT NULL,
    executed_at         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    executed_by         NVARCHAR(100) NOT NULL,
    execution_duration_ms INT,
    is_rolled_back      BIT NOT NULL DEFAULT 0,
    rolled_back_at      DATETIME2,
    rolled_back_by      NVARCHAR(100)
);

-- Template publish queue: for async DDL execution
CREATE TABLE meta.publish_queue (
    id                  INT IDENTITY(1,1) PRIMARY KEY,
    template_version_id INT NOT NULL REFERENCES meta.template_versions(id),
    status              VARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending, Processing, Completed, Failed
    ddl_script          NVARCHAR(MAX) NOT NULL,
    error_message       NVARCHAR(MAX),
    queued_at           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    queued_by           NVARCHAR(100) NOT NULL,
    processed_at        DATETIME2,
    retry_count         INT NOT NULL DEFAULT 0
);
```

### A.4 Operational Tables (kept from current design, enhanced)

```sql
-- ============================================================================
-- OPERATIONAL TABLES (reference data + submission tracking)
-- These are managed via EF Core as today
-- ============================================================================

-- institutions, return_periods, return_submissions: kept as-is from schema.sql
-- with minor column additions:

ALTER TABLE dbo.return_submissions ADD
    template_version_id INT REFERENCES meta.template_versions(id),
    raw_xml             NVARCHAR(MAX),              -- store original XML for replay
    parsed_data_json    NVARCHAR(MAX),              -- JSON snapshot of parsed data
    processing_duration_ms INT;

-- validation_reports and validation_errors: kept from current domain model
```

### A.5 Entity-Relationship Summary

```
meta.return_templates (1) --< meta.template_versions (N)
meta.template_versions (1) --< meta.template_fields (N)
meta.template_versions (1) --< meta.template_item_codes (N)
meta.template_versions (1) --< meta.template_sections (N)
meta.template_versions (1) --< meta.intra_sheet_formulas (N)
meta.cross_sheet_rules (1) --< meta.cross_sheet_rule_operands (N)
meta.cross_sheet_rules (1) --- meta.cross_sheet_rule_expressions (1)
meta.return_templates (1) --< meta.ddl_migrations (N)
meta.template_versions (1) --< meta.publish_queue (N)

dbo.return_submissions --> meta.template_versions (FK)
dbo.return_submissions --> dbo.institutions (FK)
dbo.return_submissions --> dbo.return_periods (FK)
```

---

## B. Solution Structure

```
/FC Engine/
|
+-- FCEngine.sln
|
+-- src/
|   +-- FC.Engine.Domain/
|   |   +-- FC.Engine.Domain.csproj           (no external deps except Abstractions)
|   |   +-- Entities/
|   |   |   +-- Submission.cs                 (KEPT - enhanced with TemplateVersionId)
|   |   |   +-- Institution.cs                (KEPT as-is)
|   |   |   +-- ReturnPeriod.cs               (KEPT as-is)
|   |   |   +-- ValidationReport.cs           (KEPT as-is)
|   |   |   +-- ValidationError.cs            (KEPT as-is)
|   |   +-- Metadata/
|   |   |   +-- ReturnTemplate.cs             (NEW - aggregate root for template + versions)
|   |   |   +-- TemplateVersion.cs            (NEW - version with status lifecycle)
|   |   |   +-- TemplateField.cs              (NEW - field definition value object)
|   |   |   +-- TemplateItemCode.cs           (NEW - predefined item code)
|   |   |   +-- TemplateSection.cs            (NEW - section grouping)
|   |   |   +-- TemplateStatus.cs             (NEW - lifecycle enum)
|   |   +-- Validation/
|   |   |   +-- IntraSheetFormula.cs           (NEW - formula definition entity)
|   |   |   +-- CrossSheetRule.cs              (NEW - cross-sheet rule entity)
|   |   |   +-- CrossSheetRuleOperand.cs       (NEW - operand definition)
|   |   |   +-- BusinessRule.cs                (NEW - business rule entity)
|   |   |   +-- FormulaType.cs                 (NEW - enum: Sum, Difference, Ratio, etc.)
|   |   |   +-- ValidationResult.cs            (KEPT/RENAMED)
|   |   +-- DataRecord/
|   |   |   +-- ReturnDataRecord.cs            (NEW - replaces IReturnData + all 103 typed classes)
|   |   |   +-- ReturnDataRow.cs               (NEW - single row: Dictionary<string, object?>)
|   |   |   +-- ReturnDataSet.cs               (NEW - wrapper for multi-row data)
|   |   +-- ValueObjects/
|   |   |   +-- ReturnCode.cs                  (KEPT as-is - still useful for parsing)
|   |   |   +-- ReportingPeriod.cs             (KEPT)
|   |   |   +-- FieldValue.cs                  (NEW - typed wrapper for dynamic field values)
|   |   +-- Enums/
|   |   |   +-- SubmissionStatus.cs            (KEPT as-is)
|   |   |   +-- ReturnFrequency.cs             (KEPT as-is)
|   |   |   +-- ValidationSeverity.cs          (KEPT as-is)
|   |   |   +-- ValidationCategory.cs          (KEPT as-is)
|   |   |   +-- StructuralCategory.cs          (NEW - FixedRow, MultiRow, ItemCoded)
|   |   |   +-- FieldDataType.cs               (NEW - Money, Integer, Decimal, Text, Date, Boolean, Percentage)
|   |   |   +-- FormulaType.cs                 (NEW - Sum, Difference, Equals, GreaterThan, etc.)
|   |   +-- Abstractions/
|   |       +-- ITemplateRepository.cs         (NEW)
|   |       +-- ITemplateFieldRepository.cs    (NEW)
|   |       +-- IFormulaRepository.cs          (NEW)
|   |       +-- IGenericDataRepository.cs      (NEW - replaces IReturnRepository)
|   |       +-- ISubmissionRepository.cs       (KEPT, slightly modified)
|   |       +-- IDdlEngine.cs                  (NEW)
|   |       +-- IXsdGenerator.cs               (NEW - replaces IXsdSchemaProvider)
|   |       +-- IGenericXmlParser.cs            (NEW - replaces IXmlParser)
|   |       +-- IFormulaEvaluator.cs           (NEW)
|   |       +-- ICrossSheetValidator.cs        (NEW)
|   |       +-- IBusinessRuleEvaluator.cs      (NEW)
|   |       +-- IAuditLogger.cs                (NEW)
|   |
|   +-- FC.Engine.Application/
|   |   +-- FC.Engine.Application.csproj
|   |   +-- DependencyInjection.cs
|   |   +-- Services/
|   |   |   +-- TemplateService.cs             (NEW - CRUD for templates)
|   |   |   +-- TemplateVersioningService.cs   (NEW - version lifecycle management)
|   |   |   +-- FormulaService.cs              (NEW - CRUD for validation rules)
|   |   |   +-- IngestionOrchestrator.cs       (REWRITTEN - uses generic pipeline)
|   |   |   +-- ValidationOrchestrator.cs      (NEW - coordinates all validation phases)
|   |   |   +-- SeedService.cs                 (NEW - seeds 103 templates from schema.sql)
|   |   |   +-- TemplateImpactAnalyzer.cs      (NEW - impact analysis for changes)
|   |   +-- DTOs/
|   |   |   +-- SubmissionDto.cs               (KEPT)
|   |   |   +-- SubmissionResultDto.cs         (KEPT)
|   |   |   +-- ValidationReportDto.cs         (KEPT)
|   |   |   +-- TemplateDto.cs                 (NEW)
|   |   |   +-- TemplateFieldDto.cs            (NEW)
|   |   |   +-- FormulaDto.cs                  (NEW)
|   |   |   +-- TemplateVersionDto.cs          (NEW)
|   |   |   +-- ReturnDataDto.cs               (NEW - generic data representation)
|   |
|   +-- FC.Engine.Infrastructure/
|   |   +-- FC.Engine.Infrastructure.csproj
|   |   +-- DependencyInjection.cs             (REWRITTEN)
|   |   +-- Metadata/
|   |   |   +-- MetadataDbContext.cs            (NEW - EF context for meta schema)
|   |   |   +-- Configurations/
|   |   |   |   +-- ReturnTemplateConfiguration.cs
|   |   |   |   +-- TemplateVersionConfiguration.cs
|   |   |   |   +-- TemplateFieldConfiguration.cs
|   |   |   |   +-- IntraSheetFormulaConfiguration.cs
|   |   |   |   +-- CrossSheetRuleConfiguration.cs
|   |   |   |   +-- BusinessRuleConfiguration.cs
|   |   |   |   +-- AuditLogConfiguration.cs
|   |   |   +-- Repositories/
|   |   |       +-- TemplateRepository.cs       (NEW)
|   |   |       +-- FormulaRepository.cs        (NEW)
|   |   +-- DynamicSchema/
|   |   |   +-- DdlEngine.cs                   (NEW - generates DDL from metadata)
|   |   |   +-- DdlMigrationExecutor.cs        (NEW - executes DDL safely)
|   |   |   +-- SqlTypeMapper.cs               (NEW - FieldDataType -> SQL type)
|   |   +-- Xml/
|   |   |   +-- XsdGenerator.cs                (NEW - generates XSD from template_fields)
|   |   |   +-- GenericXmlParser.cs            (NEW - parses any XML using metadata)
|   |   |   +-- XmlNamespaceResolver.cs        (NEW)
|   |   +-- Persistence/
|   |   |   +-- FcEngineDbContext.cs            (KEPT - for operational tables only)
|   |   |   +-- GenericDataRepository.cs       (NEW - Dapper-based dynamic CRUD)
|   |   |   +-- DynamicSqlBuilder.cs           (NEW - builds parameterized INSERT/SELECT)
|   |   |   +-- Repositories/
|   |   |       +-- SubmissionRepository.cs     (KEPT, modified)
|   |   +-- Validation/
|   |   |   +-- FormulaEvaluator.cs            (NEW - interprets formula metadata)
|   |   |   +-- CrossSheetValidator.cs         (NEW - evaluates cross-sheet rules)
|   |   |   +-- BusinessRuleEvaluator.cs       (NEW - evaluates business rules)
|   |   |   +-- ExpressionParser.cs            (NEW - parses "A + B - C = D" expressions)
|   |   |   +-- ExpressionTokenizer.cs         (NEW)
|   |   +-- Caching/
|   |   |   +-- TemplateMetadataCache.cs       (NEW - in-memory cache for template defs)
|   |   +-- Audit/
|   |       +-- AuditLogger.cs                 (NEW)
|   |
|   +-- FC.Engine.Api/
|   |   +-- FC.Engine.Api.csproj
|   |   +-- Program.cs                         (MODIFIED)
|   |   +-- Endpoints/
|   |   |   +-- SubmissionEndpoints.cs         (MODIFIED - uses generic pipeline)
|   |   |   +-- SchemaEndpoints.cs             (MODIFIED - returns generated XSD)
|   |   |   +-- TemplateEndpoints.cs           (NEW - REST API for template management)
|   |   |   +-- ValidationRuleEndpoints.cs     (NEW - REST API for rule management)
|   |   +-- Middleware/
|   |       +-- ExceptionHandlingMiddleware.cs  (KEPT)
|   |
|   +-- FC.Engine.Admin/
|   |   +-- FC.Engine.Admin.csproj             (NEW - Blazor Server)
|   |   +-- Program.cs
|   |   +-- Pages/
|   |   |   +-- Templates/
|   |   |   |   +-- TemplateList.razor
|   |   |   |   +-- TemplateDesigner.razor
|   |   |   |   +-- FieldEditor.razor
|   |   |   |   +-- TemplatePreview.razor
|   |   |   |   +-- VersionHistory.razor
|   |   |   +-- Validation/
|   |   |   |   +-- FormulaBuilder.razor
|   |   |   |   +-- CrossSheetRuleEditor.razor
|   |   |   |   +-- BusinessRuleEditor.razor
|   |   |   |   +-- RuleTestRunner.razor
|   |   |   +-- Submissions/
|   |   |   |   +-- SubmissionList.razor
|   |   |   |   +-- SubmissionDetail.razor
|   |   |   |   +-- ValidationReportView.razor
|   |   |   +-- Audit/
|   |   |   |   +-- AuditLog.razor
|   |   |   |   +-- ImpactAnalysis.razor
|   |   |   +-- Dashboard.razor
|   |   +-- Components/
|   |   |   +-- FieldDefinitionGrid.razor
|   |   |   +-- FormulaExpressionEditor.razor
|   |   |   +-- TemplateStatusBadge.razor
|   |   |   +-- DataTypeSelector.razor
|   |   |   +-- XmlPreviewPanel.razor
|   |   +-- wwwroot/
|   |
|   +-- FC.Engine.Migrator/
|       +-- FC.Engine.Migrator.csproj
|       +-- Program.cs                         (REWRITTEN - seeds metadata + runs DDL)
|       +-- SeedData/
|           +-- TemplateSeedData.cs             (NEW - parses schema.sql into metadata)
|           +-- FormulaSeedData.cs             (NEW - parses formula_catalog into rules)
|
+-- tests/
|   +-- FC.Engine.Domain.Tests/
|   +-- FC.Engine.Infrastructure.Tests/
|   |   +-- DdlEngineTests.cs                 (NEW)
|   |   +-- XsdGeneratorTests.cs              (NEW)
|   |   +-- GenericXmlParserTests.cs          (NEW)
|   |   +-- GenericDataRepositoryTests.cs     (NEW)
|   |   +-- FormulaEvaluatorTests.cs          (NEW)
|   |   +-- ExpressionParserTests.cs          (NEW)
|   |   +-- CrossSheetValidatorTests.cs       (NEW)
|   +-- FC.Engine.Integration.Tests/
|   +-- FC.Engine.Admin.Tests/                (NEW)
|
+-- docker/
|   +-- docker-compose.yml                    (MODIFIED - add admin service)
|   +-- Dockerfile.api
|   +-- Dockerfile.admin                      (NEW)
|   +-- Dockerfile.migrator
|
+-- scripts/
    +-- seed-reference-data.sql               (KEPT)
    +-- seed-template-metadata.sql            (NEW - generated by SeedService)
```

---

## C. Core Components Design

### C.1 ReturnDataRecord -- The Universal Data Container

This replaces all 103 `IReturnData` implementations. The key insight: return data is fundamentally a dictionary of field names to values, with the template metadata providing the schema.

```csharp
// FC.Engine.Domain/DataRecord/ReturnDataRecord.cs
namespace FC.Engine.Domain.DataRecord;

/// <summary>
/// Universal container for return data. Replaces all 103 per-template IReturnData classes.
/// For FixedRow: contains exactly one row.
/// For MultiRow: contains N rows, each identified by serial_no.
/// For ItemCoded: contains N rows, each identified by item_code.
/// </summary>
public class ReturnDataRecord
{
    public string ReturnCode { get; }
    public int TemplateVersionId { get; }
    public StructuralCategory Category { get; }
    private readonly List<ReturnDataRow> _rows = new();
    
    public IReadOnlyList<ReturnDataRow> Rows => _rows.AsReadOnly();
    
    public ReturnDataRecord(string returnCode, int templateVersionId, StructuralCategory category)
    {
        ReturnCode = returnCode;
        TemplateVersionId = templateVersionId;
        Category = category;
    }
    
    public void AddRow(ReturnDataRow row) => _rows.Add(row);
    
    /// <summary>
    /// For FixedRow templates: get the single data row.
    /// </summary>
    public ReturnDataRow SingleRow => Category == StructuralCategory.FixedRow
        ? _rows.Single()
        : throw new InvalidOperationException("SingleRow only valid for FixedRow templates");
    
    /// <summary>
    /// Get a field value from the single row (FixedRow) or by row key (MultiRow/ItemCoded).
    /// </summary>
    public object? GetValue(string fieldName, string? rowKey = null)
    {
        var row = rowKey == null ? SingleRow : _rows.FirstOrDefault(r => r.RowKey == rowKey);
        return row?.GetValue(fieldName);
    }
    
    public decimal? GetDecimal(string fieldName, string? rowKey = null)
    {
        var val = GetValue(fieldName, rowKey);
        return val switch
        {
            null => null,
            decimal d => d,
            _ => Convert.ToDecimal(val)
        };
    }
}

/// <summary>
/// A single row of return data. Each field is stored by name.
/// </summary>
public class ReturnDataRow
{
    private readonly Dictionary<string, object?> _fields = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Row identity: serial_no for MultiRow, item_code for ItemCoded, null for FixedRow.
    /// </summary>
    public string? RowKey { get; set; }
    
    public void SetValue(string fieldName, object? value) => _fields[fieldName] = value;
    
    public object? GetValue(string fieldName) =>
        _fields.TryGetValue(fieldName, out var val) ? val : null;
    
    public bool HasField(string fieldName) => _fields.ContainsKey(fieldName);
    
    public IReadOnlyDictionary<string, object?> AllFields => _fields;
    
    public decimal? GetDecimal(string fieldName)
    {
        var val = GetValue(fieldName);
        return val switch
        {
            null => null,
            decimal d => d,
            int i => i,
            long l => l,
            string s when decimal.TryParse(s, out var d) => d,
            _ => Convert.ToDecimal(val)
        };
    }
}
```

### C.2 GenericXmlParser -- Parses Any XML Using Metadata

```csharp
// FC.Engine.Infrastructure/Xml/GenericXmlParser.cs
namespace FC.Engine.Infrastructure.Xml;

public class GenericXmlParser : IGenericXmlParser
{
    private readonly ITemplateMetadataCache _metadataCache;
    
    public GenericXmlParser(ITemplateMetadataCache metadataCache)
    {
        _metadataCache = metadataCache;
    }
    
    public async Task<ReturnDataRecord> Parse(Stream xmlStream, string returnCode, CancellationToken ct)
    {
        // 1. Load template metadata (cached)
        var template = await _metadataCache.GetPublishedTemplate(returnCode, ct);
        var version = template.CurrentVersion;
        var fields = version.Fields;
        
        // 2. Parse XML
        var doc = XDocument.Load(xmlStream);
        XNamespace ns = template.XmlNamespace;
        var root = doc.Root!;
        
        var record = new ReturnDataRecord(returnCode, version.Id, template.StructuralCategory);
        
        switch (template.StructuralCategory)
        {
            case StructuralCategory.FixedRow:
                ParseFixedRow(root, ns, fields, record);
                break;
                
            case StructuralCategory.MultiRow:
                ParseMultiRow(root, ns, fields, record);
                break;
                
            case StructuralCategory.ItemCoded:
                ParseItemCoded(root, ns, fields, record, version.ItemCodes);
                break;
        }
        
        return record;
    }
    
    private void ParseFixedRow(XElement root, XNamespace ns,
        IReadOnlyList<TemplateField> fields, ReturnDataRecord record)
    {
        var row = new ReturnDataRow();
        
        // Navigate to the data section (first child after Header)
        var dataSection = root.Elements()
            .FirstOrDefault(e => e.Name.LocalName != "Header") ?? root;
        
        foreach (var field in fields)
        {
            var element = dataSection.Element(ns + field.XmlElementName)
                       ?? dataSection.Descendants(ns + field.XmlElementName).FirstOrDefault();
            
            if (element != null && !string.IsNullOrWhiteSpace(element.Value))
            {
                row.SetValue(field.FieldName, ConvertValue(element.Value, field.DataType));
            }
        }
        
        record.AddRow(row);
    }
    
    private void ParseMultiRow(XElement root, XNamespace ns,
        IReadOnlyList<TemplateField> fields, ReturnDataRecord record)
    {
        // Multi-row templates have a repeating element (e.g., <Row> or <Item>)
        var dataSection = root.Elements()
            .FirstOrDefault(e => e.Name.LocalName != "Header") ?? root;
        
        var rowElements = dataSection.Elements().Where(e => e.Name.LocalName != "Header");
        int serialNo = 1;
        
        foreach (var rowElement in rowElements)
        {
            var row = new ReturnDataRow { RowKey = serialNo.ToString() };
            
            foreach (var field in fields)
            {
                if (field.FieldName == "serial_no")
                {
                    row.SetValue("serial_no", serialNo);
                    continue;
                }
                
                var element = rowElement.Element(ns + field.XmlElementName);
                if (element != null && !string.IsNullOrWhiteSpace(element.Value))
                {
                    row.SetValue(field.FieldName, ConvertValue(element.Value, field.DataType));
                }
            }
            
            record.AddRow(row);
            serialNo++;
        }
    }
    
    private void ParseItemCoded(XElement root, XNamespace ns,
        IReadOnlyList<TemplateField> fields, ReturnDataRecord record,
        IReadOnlyList<TemplateItemCode> expectedItemCodes)
    {
        var dataSection = root.Elements()
            .FirstOrDefault(e => e.Name.LocalName != "Header") ?? root;
        
        var rowElements = dataSection.Elements().Where(e => e.Name.LocalName != "Header");
        
        foreach (var rowElement in rowElements)
        {
            var itemCodeField = fields.FirstOrDefault(f => f.IsKeyField);
            var itemCodeElement = rowElement.Element(ns + (itemCodeField?.XmlElementName ?? "ItemCode"));
            var itemCodeValue = itemCodeElement?.Value;
            
            if (string.IsNullOrWhiteSpace(itemCodeValue)) continue;
            
            var row = new ReturnDataRow { RowKey = itemCodeValue };
            row.SetValue(itemCodeField?.FieldName ?? "item_code", itemCodeValue);
            
            foreach (var field in fields.Where(f => !f.IsKeyField))
            {
                var element = rowElement.Element(ns + field.XmlElementName);
                if (element != null && !string.IsNullOrWhiteSpace(element.Value))
                {
                    row.SetValue(field.FieldName, ConvertValue(element.Value, field.DataType));
                }
            }
            
            record.AddRow(row);
        }
    }
    
    private static object? ConvertValue(string text, FieldDataType dataType)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        
        return dataType switch
        {
            FieldDataType.Money or FieldDataType.Decimal or FieldDataType.Percentage
                => decimal.TryParse(text, out var d) ? d : null,
            FieldDataType.Integer
                => int.TryParse(text, out var i) ? i : null,
            FieldDataType.Date
                => DateTime.TryParse(text, out var dt) ? dt : null,
            FieldDataType.Boolean
                => bool.TryParse(text, out var b) ? b : null,
            FieldDataType.Text
                => text,
            _ => text
        };
    }
}
```

### C.3 XsdGenerator -- Generates XSD From Metadata at Runtime

```csharp
// FC.Engine.Infrastructure/Xml/XsdGenerator.cs
namespace FC.Engine.Infrastructure.Xml;

public class XsdGenerator : IXsdGenerator
{
    private readonly ITemplateMetadataCache _cache;
    private readonly ConcurrentDictionary<string, XmlSchemaSet> _xsdCache = new();
    
    public async Task<XmlSchemaSet> GenerateSchema(string returnCode, CancellationToken ct)
    {
        if (_xsdCache.TryGetValue(returnCode, out var cached))
            return cached;
        
        var template = await _cache.GetPublishedTemplate(returnCode, ct);
        var version = template.CurrentVersion;
        var fields = version.Fields;
        
        var xsd = new StringBuilder();
        xsd.AppendLine($@"<?xml version=""1.0"" encoding=""UTF-8""?>");
        xsd.AppendLine($@"<xs:schema xmlns:xs=""http://www.w3.org/2001/XMLSchema""");
        xsd.AppendLine($@"           targetNamespace=""{template.XmlNamespace}""");
        xsd.AppendLine($@"           xmlns:tns=""{template.XmlNamespace}""");
        xsd.AppendLine($@"           elementFormDefault=""qualified"">");
        
        // Root element
        xsd.AppendLine($@"  <xs:element name=""{template.XmlRootElement}"">");
        xsd.AppendLine($@"    <xs:complexType>");
        xsd.AppendLine($@"      <xs:sequence>");
        xsd.AppendLine($@"        <xs:element name=""Header"" type=""tns:HeaderType""/>");
        
        if (template.StructuralCategory == StructuralCategory.FixedRow)
        {
            xsd.AppendLine($@"        <xs:element name=""Data"" type=""tns:DataType""/>");
        }
        else
        {
            xsd.AppendLine($@"        <xs:element name=""Rows"">");
            xsd.AppendLine($@"          <xs:complexType>");
            xsd.AppendLine($@"            <xs:sequence>");
            xsd.AppendLine($@"              <xs:element name=""Row"" type=""tns:RowType"" ");
            xsd.AppendLine($@"                          maxOccurs=""unbounded"" minOccurs=""0""/>");
            xsd.AppendLine($@"            </xs:sequence>");
            xsd.AppendLine($@"          </xs:complexType>");
            xsd.AppendLine($@"        </xs:element>");
        }
        
        xsd.AppendLine($@"      </xs:sequence>");
        xsd.AppendLine($@"    </xs:complexType>");
        xsd.AppendLine($@"  </xs:element>");
        
        // Header type (standard for all templates)
        xsd.AppendLine(@"  <xs:complexType name=""HeaderType"">");
        xsd.AppendLine(@"    <xs:sequence>");
        xsd.AppendLine(@"      <xs:element name=""InstitutionCode"" type=""xs:string""/>");
        xsd.AppendLine(@"      <xs:element name=""ReportingDate"" type=""xs:date""/>");
        xsd.AppendLine($@"      <xs:element name=""ReturnCode"" type=""xs:string"" fixed=""{template.ReturnCode.Replace(" ", "")}""/>");
        xsd.AppendLine(@"    </xs:sequence>");
        xsd.AppendLine(@"  </xs:complexType>");
        
        // Data/Row type with all fields
        var typeName = template.StructuralCategory == StructuralCategory.FixedRow ? "DataType" : "RowType";
        xsd.AppendLine($@"  <xs:complexType name=""{typeName}"">");
        xsd.AppendLine(@"    <xs:sequence>");
        
        foreach (var field in fields.OrderBy(f => f.FieldOrder))
        {
            var xsdType = MapToXsdType(field.DataType, field.SqlType);
            var minOccurs = field.IsRequired ? "1" : "0";
            xsd.AppendLine($@"      <xs:element name=""{field.XmlElementName}"" type=""{xsdType}"" minOccurs=""{minOccurs}""/>");
        }
        
        xsd.AppendLine(@"    </xs:sequence>");
        xsd.AppendLine(@"  </xs:complexType>");
        
        // Custom simple types
        xsd.AppendLine(@"  <xs:simpleType name=""MoneyType"">");
        xsd.AppendLine(@"    <xs:restriction base=""xs:decimal"">");
        xsd.AppendLine(@"      <xs:fractionDigits value=""2""/>");
        xsd.AppendLine(@"      <xs:totalDigits value=""20""/>");
        xsd.AppendLine(@"    </xs:restriction>");
        xsd.AppendLine(@"  </xs:simpleType>");
        
        xsd.AppendLine(@"</xs:schema>");
        
        // Parse and cache
        var schemaSet = new XmlSchemaSet();
        using var reader = new StringReader(xsd.ToString());
        schemaSet.Add(XmlSchema.Read(reader, null)!);
        schemaSet.Compile();
        
        _xsdCache[returnCode] = schemaSet;
        return schemaSet;
    }
    
    private static string MapToXsdType(FieldDataType dataType, string sqlType) => dataType switch
    {
        FieldDataType.Money => "tns:MoneyType",
        FieldDataType.Decimal => "xs:decimal",
        FieldDataType.Percentage => "xs:decimal",
        FieldDataType.Integer => "xs:int",
        FieldDataType.Text => "xs:string",
        FieldDataType.Date => "xs:date",
        FieldDataType.Boolean => "xs:boolean",
        _ => "xs:string"
    };
    
    public void InvalidateCache(string returnCode)
    {
        _xsdCache.TryRemove(returnCode, out _);
    }
}
```

### C.4 DdlEngine -- Generates CREATE TABLE / ALTER TABLE From Metadata

```csharp
// FC.Engine.Infrastructure/DynamicSchema/DdlEngine.cs
namespace FC.Engine.Infrastructure.DynamicSchema;

public class DdlEngine : IDdlEngine
{
    private readonly SqlTypeMapper _typeMapper;
    
    public DdlEngine(SqlTypeMapper typeMapper)
    {
        _typeMapper = typeMapper;
    }
    
    /// <summary>
    /// Generate CREATE TABLE DDL for a new template version.
    /// </summary>
    public DdlScript GenerateCreateTable(ReturnTemplate template, TemplateVersion version)
    {
        var tableName = template.PhysicalTableName;
        var fields = version.Fields;
        var sb = new StringBuilder();
        
        sb.AppendLine($"CREATE TABLE dbo.[{tableName}] (");
        sb.AppendLine("    id INT IDENTITY(1,1) PRIMARY KEY,");
        sb.AppendLine("    submission_id INT NOT NULL REFERENCES dbo.return_submissions(id),");
        
        foreach (var field in fields.OrderBy(f => f.FieldOrder))
        {
            var sqlType = _typeMapper.ToSqlType(field.DataType, field.SqlType);
            var nullable = field.IsRequired ? " NOT NULL" : "";
            var defaultVal = !string.IsNullOrEmpty(field.DefaultValue)
                ? $" DEFAULT {field.DefaultValue}" : "";
            sb.AppendLine($"    [{field.FieldName}] {sqlType}{nullable}{defaultVal},");
        }
        
        sb.AppendLine("    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()");
        sb.AppendLine(");");
        sb.AppendLine();
        sb.AppendLine($"CREATE INDEX IX_{tableName}_submission ON dbo.[{tableName}](submission_id);");
        
        // Rollback script
        var rollback = $"DROP TABLE IF EXISTS dbo.[{tableName}];";
        
        return new DdlScript(sb.ToString(), rollback);
    }
    
    /// <summary>
    /// Generate ALTER TABLE DDL to migrate from one version to another.
    /// Compares field lists and generates ADD COLUMN / ALTER COLUMN statements.
    /// Does NOT generate DROP COLUMN (data preservation).
    /// </summary>
    public DdlScript GenerateAlterTable(
        ReturnTemplate template,
        TemplateVersion oldVersion,
        TemplateVersion newVersion)
    {
        var tableName = template.PhysicalTableName;
        var oldFields = oldVersion.Fields.ToDictionary(f => f.FieldName, StringComparer.OrdinalIgnoreCase);
        var newFields = newVersion.Fields.ToDictionary(f => f.FieldName, StringComparer.OrdinalIgnoreCase);
        
        var sb = new StringBuilder();
        var rollback = new StringBuilder();
        
        // Fields added in new version
        foreach (var (name, field) in newFields)
        {
            if (!oldFields.ContainsKey(name))
            {
                var sqlType = _typeMapper.ToSqlType(field.DataType, field.SqlType);
                sb.AppendLine($"ALTER TABLE dbo.[{tableName}] ADD [{name}] {sqlType};");
                rollback.AppendLine($"ALTER TABLE dbo.[{tableName}] DROP COLUMN [{name}];");
            }
        }
        
        // Fields with changed data types (widen only -- never narrow)
        foreach (var (name, newField) in newFields)
        {
            if (oldFields.TryGetValue(name, out var oldField))
            {
                if (oldField.SqlType != newField.SqlType)
                {
                    var newSqlType = _typeMapper.ToSqlType(newField.DataType, newField.SqlType);
                    var oldSqlType = _typeMapper.ToSqlType(oldField.DataType, oldField.SqlType);
                    
                    // Safety: only allow widening conversions
                    if (IsWideningConversion(oldSqlType, newSqlType))
                    {
                        sb.AppendLine($"ALTER TABLE dbo.[{tableName}] ALTER COLUMN [{name}] {newSqlType};");
                        rollback.AppendLine($"ALTER TABLE dbo.[{tableName}] ALTER COLUMN [{name}] {oldSqlType};");
                    }
                }
            }
        }
        
        // Fields removed: mark as deprecated in metadata but do NOT drop column
        // (data preservation principle)
        foreach (var (name, _) in oldFields)
        {
            if (!newFields.ContainsKey(name))
            {
                sb.AppendLine($"-- NOTE: Field [{name}] removed from template but column preserved in table");
            }
        }
        
        return new DdlScript(sb.ToString(), rollback.ToString());
    }
    
    private static bool IsWideningConversion(string oldType, string newType)
    {
        // Allow: VARCHAR(x) -> VARCHAR(y) where y > x
        // Allow: DECIMAL(p1,s1) -> DECIMAL(p2,s2) where p2 >= p1
        // Allow: INT -> BIGINT
        // Deny: anything else (requires manual intervention)
        // Simplified for example:
        return true; // Full implementation compares precision/length
    }
}

public record DdlScript(string ForwardSql, string RollbackSql);
```

### C.5 GenericDataRepository -- Dynamic CRUD With Dapper

```csharp
// FC.Engine.Infrastructure/Persistence/GenericDataRepository.cs
namespace FC.Engine.Infrastructure.Persistence;

/// <summary>
/// Replaces all 103 per-template EF entity classes + configurations + repository switch branches.
/// Uses Dapper for dynamic parameterized SQL against physical tables.
/// </summary>
public class GenericDataRepository : IGenericDataRepository
{
    private readonly IDbConnection _connection;
    private readonly ITemplateMetadataCache _cache;
    private readonly DynamicSqlBuilder _sqlBuilder;
    
    public GenericDataRepository(
        IDbConnection connection,
        ITemplateMetadataCache cache,
        DynamicSqlBuilder sqlBuilder)
    {
        _connection = connection;
        _cache = cache;
        _sqlBuilder = sqlBuilder;
    }
    
    public async Task Save(ReturnDataRecord record, int submissionId, CancellationToken ct)
    {
        var template = await _cache.GetPublishedTemplate(record.ReturnCode, ct);
        var tableName = template.PhysicalTableName;
        var fields = template.CurrentVersion.Fields;
        
        foreach (var row in record.Rows)
        {
            var (sql, parameters) = _sqlBuilder.BuildInsert(tableName, fields, row, submissionId);
            await _connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }
    }
    
    public async Task<ReturnDataRecord?> GetBySubmission(
        string returnCode, int submissionId, CancellationToken ct)
    {
        var template = await _cache.GetPublishedTemplate(returnCode, ct);
        var tableName = template.PhysicalTableName;
        var fields = template.CurrentVersion.Fields;
        
        var sql = _sqlBuilder.BuildSelect(tableName, fields, submissionId);
        var rows = await _connection.QueryAsync(new CommandDefinition(sql,
            new { submissionId }, cancellationToken: ct));
        
        if (!rows.Any()) return null;
        
        var record = new ReturnDataRecord(returnCode, template.CurrentVersion.Id,
            template.StructuralCategory);
        
        foreach (IDictionary<string, object> dbRow in rows)
        {
            var dataRow = new ReturnDataRow();
            
            // Determine row key based on structural category
            if (template.StructuralCategory == StructuralCategory.MultiRow)
                dataRow.RowKey = dbRow.GetValueOrDefault("serial_no")?.ToString();
            else if (template.StructuralCategory == StructuralCategory.ItemCoded)
                dataRow.RowKey = dbRow.GetValueOrDefault("item_code")?.ToString();
            
            foreach (var field in fields)
            {
                if (dbRow.TryGetValue(field.FieldName, out var value))
                    dataRow.SetValue(field.FieldName, value);
            }
            
            record.AddRow(dataRow);
        }
        
        return record;
    }
    
    public async Task DeleteBySubmission(string returnCode, int submissionId, CancellationToken ct)
    {
        var template = await _cache.GetPublishedTemplate(returnCode, ct);
        var sql = $"DELETE FROM dbo.[{template.PhysicalTableName}] WHERE submission_id = @submissionId";
        await _connection.ExecuteAsync(new CommandDefinition(sql,
            new { submissionId }, cancellationToken: ct));
    }
}

// FC.Engine.Infrastructure/Persistence/DynamicSqlBuilder.cs
public class DynamicSqlBuilder
{
    public (string Sql, DynamicParameters Parameters) BuildInsert(
        string tableName,
        IReadOnlyList<TemplateField> fields,
        ReturnDataRow row,
        int submissionId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@submission_id", submissionId);
        
        var columns = new List<string> { "submission_id" };
        var paramNames = new List<string> { "@submission_id" };
        
        foreach (var field in fields)
        {
            var value = row.GetValue(field.FieldName);
            if (value != null)
            {
                var paramName = $"@{field.FieldName}";
                columns.Add($"[{field.FieldName}]");
                paramNames.Add(paramName);
                parameters.Add(paramName, value);
            }
        }
        
        var sql = $"INSERT INTO dbo.[{tableName}] ({string.Join(", ", columns)}) " +
                  $"VALUES ({string.Join(", ", paramNames)})";
        
        return (sql, parameters);
    }
    
    public string BuildSelect(string tableName, IReadOnlyList<TemplateField> fields, int submissionId)
    {
        var columns = fields.Select(f => $"[{f.FieldName}]").ToList();
        columns.Insert(0, "id");
        columns.Insert(1, "submission_id");
        
        return $"SELECT {string.Join(", ", columns)} FROM dbo.[{tableName}] " +
               $"WHERE submission_id = @submissionId ORDER BY id";
    }
}
```

### C.6 FormulaEvaluator -- Interprets Intra-Sheet Formulas From Metadata

```csharp
// FC.Engine.Infrastructure/Validation/FormulaEvaluator.cs
namespace FC.Engine.Infrastructure.Validation;

/// <summary>
/// Replaces all 103+ per-template IIntraSheetRule implementations.
/// Reads formula definitions from metadata and evaluates them against ReturnDataRecord.
/// </summary>
public class FormulaEvaluator : IFormulaEvaluator
{
    private readonly ITemplateMetadataCache _cache;
    private readonly ExpressionParser _expressionParser;
    
    public FormulaEvaluator(ITemplateMetadataCache cache, ExpressionParser expressionParser)
    {
        _cache = cache;
        _expressionParser = expressionParser;
    }
    
    public async Task<IReadOnlyList<ValidationError>> Evaluate(
        ReturnDataRecord record, CancellationToken ct)
    {
        var errors = new List<ValidationError>();
        var template = await _cache.GetPublishedTemplate(record.ReturnCode, ct);
        var formulas = template.CurrentVersion.IntraSheetFormulas.Where(f => f.IsActive);
        
        foreach (var formula in formulas)
        {
            switch (template.StructuralCategory)
            {
                case StructuralCategory.FixedRow:
                    EvaluateForRow(formula, record.SingleRow, errors);
                    break;
                    
                case StructuralCategory.MultiRow:
                case StructuralCategory.ItemCoded:
                    // For multi-row, formulas can apply per-row or across all rows
                    foreach (var row in record.Rows)
                    {
                        EvaluateForRow(formula, row, errors, row.RowKey);
                    }
                    break;
            }
        }
        
        return errors;
    }
    
    private void EvaluateForRow(
        IntraSheetFormula formula,
        ReturnDataRow row,
        List<ValidationError> errors,
        string? rowKey = null)
    {
        var targetValue = row.GetDecimal(formula.TargetFieldName);
        if (targetValue == null && formula.FormulaType != FormulaType.Required) return;
        
        var operandNames = JsonSerializer.Deserialize<string[]>(formula.OperandFields)!;
        var operandValues = operandNames.Select(name => row.GetDecimal(name)).ToArray();
        
        bool isValid;
        decimal? expectedValue = null;
        
        switch (formula.FormulaType)
        {
            case FormulaType.Sum:
                expectedValue = operandValues.Where(v => v.HasValue).Sum(v => v!.Value);
                isValid = IsWithinTolerance(targetValue, expectedValue, formula);
                break;
                
            case FormulaType.Difference:
                // First operand minus remaining operands
                if (operandValues.Length >= 2 && operandValues[0].HasValue)
                {
                    expectedValue = operandValues[0]!.Value -
                        operandValues.Skip(1).Where(v => v.HasValue).Sum(v => v!.Value);
                    isValid = IsWithinTolerance(targetValue, expectedValue, formula);
                }
                else
                {
                    isValid = true; // skip if insufficient data
                }
                break;
                
            case FormulaType.Equals:
                expectedValue = operandValues.FirstOrDefault(v => v.HasValue);
                isValid = IsWithinTolerance(targetValue, expectedValue, formula);
                break;
                
            case FormulaType.GreaterThan:
                isValid = targetValue > operandValues.FirstOrDefault();
                break;
                
            case FormulaType.GreaterThanOrEqual:
                isValid = targetValue >= operandValues.FirstOrDefault();
                break;
                
            case FormulaType.LessThan:
                isValid = targetValue < operandValues.FirstOrDefault();
                break;
                
            case FormulaType.Between:
                isValid = operandValues.Length >= 2
                    && targetValue >= operandValues[0]
                    && targetValue <= operandValues[1];
                break;
                
            case FormulaType.Ratio:
                if (operandValues.Length >= 2 && operandValues[1].HasValue && operandValues[1] != 0)
                {
                    expectedValue = operandValues[0] / operandValues[1];
                    isValid = IsWithinTolerance(targetValue, expectedValue, formula);
                }
                else
                {
                    isValid = true;
                }
                break;
                
            case FormulaType.Custom:
                // Use expression parser for custom formulas like "A + B - C"
                var context = new Dictionary<string, decimal?>();
                for (int i = 0; i < operandNames.Length; i++)
                    context[operandNames[i]] = operandValues[i];
                
                expectedValue = _expressionParser.Evaluate(formula.CustomExpression!, context);
                isValid = IsWithinTolerance(targetValue, expectedValue, formula);
                break;
                
            case FormulaType.Required:
                isValid = targetValue.HasValue;
                break;
                
            default:
                isValid = true;
                break;
        }
        
        if (!isValid)
        {
            var rowRef = rowKey != null ? $" (row: {rowKey})" : "";
            errors.Add(new ValidationError
            {
                RuleId = formula.RuleCode,
                Field = formula.TargetFieldName + rowRef,
                Message = formula.ErrorMessage ??
                    $"Validation failed for {formula.RuleName}{rowRef}",
                ExpectedValue = expectedValue?.ToString("N2"),
                ActualValue = targetValue?.ToString("N2"),
                Severity = Enum.Parse<ValidationSeverity>(formula.Severity),
                Category = ValidationCategory.IntraSheet
            });
        }
    }
    
    private static bool IsWithinTolerance(
        decimal? actual, decimal? expected, IntraSheetFormula formula)
    {
        if (actual == null || expected == null) return true;
        
        var diff = Math.Abs(actual.Value - expected.Value);
        
        if (formula.ToleranceAmount > 0)
            return diff <= formula.ToleranceAmount;
        
        if (formula.TolerancePercent.HasValue && expected.Value != 0)
            return diff / Math.Abs(expected.Value) <= formula.TolerancePercent.Value / 100m;
        
        return actual.Value == expected.Value;
    }
}
```

### C.7 CrossSheetValidator

```csharp
// FC.Engine.Infrastructure/Validation/CrossSheetValidator.cs
namespace FC.Engine.Infrastructure.Validation;

public class CrossSheetValidator : ICrossSheetValidator
{
    private readonly IFormulaRepository _formulaRepo;
    private readonly IGenericDataRepository _dataRepo;
    private readonly ExpressionParser _parser;
    
    public async Task<IReadOnlyList<ValidationError>> Validate(
        ReturnDataRecord currentRecord,
        int institutionId,
        int returnPeriodId,
        CancellationToken ct)
    {
        var errors = new List<ValidationError>();
        
        // Find all active cross-sheet rules that involve this template
        var rules = await _formulaRepo.GetCrossSheetRulesForTemplate(
            currentRecord.ReturnCode, ct);
        
        foreach (var rule in rules)
        {
            // Build operand values
            var operandValues = new Dictionary<string, decimal?>();
            
            foreach (var operand in rule.Operands)
            {
                ReturnDataRecord? sourceData;
                
                if (operand.TemplateReturnCode == currentRecord.ReturnCode)
                {
                    sourceData = currentRecord;
                }
                else
                {
                    // Load the other template's data for same institution/period
                    sourceData = await _dataRepo.GetByInstitutionAndPeriod(
                        operand.TemplateReturnCode, institutionId, returnPeriodId, ct);
                }
                
                if (sourceData == null)
                {
                    operandValues[operand.OperandAlias] = null;
                    continue;
                }
                
                // Apply aggregate function for multi-row templates
                decimal? value;
                if (!string.IsNullOrEmpty(operand.AggregateFunction))
                {
                    var allValues = sourceData.Rows
                        .Select(r => r.GetDecimal(operand.FieldName))
                        .Where(v => v.HasValue)
                        .Select(v => v!.Value);
                    
                    value = operand.AggregateFunction switch
                    {
                        "SUM" => allValues.Any() ? allValues.Sum() : 0m,
                        "COUNT" => allValues.Count(),
                        "MAX" => allValues.Any() ? allValues.Max() : null,
                        "MIN" => allValues.Any() ? allValues.Min() : null,
                        "AVG" => allValues.Any() ? allValues.Average() : null,
                        _ => null
                    };
                }
                else if (!string.IsNullOrEmpty(operand.FilterItemCode))
                {
                    value = sourceData.GetDecimal(operand.FieldName, operand.FilterItemCode);
                }
                else
                {
                    value = sourceData.GetDecimal(operand.FieldName);
                }
                
                operandValues[operand.OperandAlias] = value;
            }
            
            // Evaluate expression
            if (operandValues.Values.All(v => v == null)) continue; // skip if no data
            
            var expression = rule.Expression;
            var result = _parser.EvaluateComparison(expression.Expression, operandValues);
            
            if (!result.IsValid)
            {
                errors.Add(new ValidationError
                {
                    RuleId = rule.RuleCode,
                    Field = $"Cross-sheet: {rule.RuleName}",
                    Message = expression.ErrorMessage ??
                        $"Cross-sheet validation failed: {rule.RuleName}",
                    Severity = Enum.Parse<ValidationSeverity>(rule.Severity),
                    Category = ValidationCategory.CrossSheet,
                    ExpectedValue = result.ExpectedValue?.ToString("N2"),
                    ActualValue = result.ActualValue?.ToString("N2")
                });
            }
        }
        
        return errors;
    }
}
```

### C.8 ExpressionParser -- Simple Expression Language

```csharp
// FC.Engine.Infrastructure/Validation/ExpressionParser.cs
namespace FC.Engine.Infrastructure.Validation;

/// <summary>
/// Parses and evaluates simple arithmetic expressions like "A + B - C" and
/// comparison expressions like "A = B + C" or "A >= B * 0.125".
/// 
/// Supported operators: +, -, *, /, (, )
/// Comparison operators: =, !=, >, <, >=, <=
/// Variables: field names or single-letter aliases (A, B, C)
/// Literals: numeric constants
/// </summary>
public class ExpressionParser
{
    /// <summary>
    /// Evaluate an arithmetic expression and return the computed value.
    /// Used for custom intra-sheet formulas.
    /// </summary>
    public decimal? Evaluate(string expression, Dictionary<string, decimal?> variables)
    {
        var tokens = Tokenize(expression);
        var postfix = InfixToPostfix(tokens);
        return EvaluatePostfix(postfix, variables);
    }
    
    /// <summary>
    /// Evaluate a comparison expression like "A = B + C" or "A >= B * 0.125".
    /// Returns whether the comparison holds, plus the actual and expected values.
    /// </summary>
    public ComparisonResult EvaluateComparison(
        string expression, Dictionary<string, decimal?> variables)
    {
        // Split on comparison operator
        var (leftExpr, op, rightExpr) = SplitComparison(expression);
        
        var leftValue = Evaluate(leftExpr, variables);
        var rightValue = Evaluate(rightExpr, variables);
        
        if (leftValue == null || rightValue == null)
            return new ComparisonResult(true, leftValue, rightValue); // skip null
        
        bool isValid = op switch
        {
            "=" or "==" => leftValue == rightValue,
            "!=" => leftValue != rightValue,
            ">" => leftValue > rightValue,
            ">=" => leftValue >= rightValue,
            "<" => leftValue < rightValue,
            "<=" => leftValue <= rightValue,
            _ => throw new ArgumentException($"Unknown comparison operator: {op}")
        };
        
        return new ComparisonResult(isValid, leftValue, rightValue);
    }
    
    // Standard shunting-yard algorithm for tokenization and evaluation
    private List<Token> Tokenize(string expr) { /* ... standard tokenizer ... */ }
    private Queue<Token> InfixToPostfix(List<Token> tokens) { /* ... shunting-yard ... */ }
    private decimal? EvaluatePostfix(Queue<Token> postfix, Dictionary<string, decimal?> vars) { /* ... */ }
    private (string Left, string Op, string Right) SplitComparison(string expr) { /* ... */ }
}

public record ComparisonResult(bool IsValid, decimal? ActualValue, decimal? ExpectedValue);
```

### C.9 ValidationOrchestrator -- Coordinates All Phases

```csharp
// FC.Engine.Application/Services/ValidationOrchestrator.cs
namespace FC.Engine.Application.Services;

public class ValidationOrchestrator
{
    private readonly IFormulaEvaluator _formulaEvaluator;
    private readonly ICrossSheetValidator _crossSheetValidator;
    private readonly IBusinessRuleEvaluator _businessRuleEvaluator;
    
    public async Task<ValidationReport> ValidateAll(
        ReturnDataRecord record,
        Submission submission,
        CancellationToken ct)
    {
        var report = ValidationReport.Create(submission.Id);
        
        // Phase 1: Type/Range validation (from field metadata: min, max, allowed_values)
        var typeErrors = await ValidateTypesAndRanges(record, ct);
        report.AddErrors(typeErrors);
        
        // Phase 2: Intra-sheet formulas
        var formulaErrors = await _formulaEvaluator.Evaluate(record, ct);
        report.AddErrors(formulaErrors);
        
        // Phase 3: Cross-sheet validation
        var crossErrors = await _crossSheetValidator.Validate(
            record, submission.InstitutionId, submission.ReturnPeriodId, ct);
        report.AddErrors(crossErrors);
        
        // Phase 4: Business rules
        var bizErrors = await _businessRuleEvaluator.Evaluate(
            record, submission, ct);
        report.AddErrors(bizErrors);
        
        report.FinalizeAt(DateTime.UtcNow);
        return report;
    }
    
    private async Task<IReadOnlyList<ValidationError>> ValidateTypesAndRanges(
        ReturnDataRecord record, CancellationToken ct)
    {
        var errors = new List<ValidationError>();
        var template = await _cache.GetPublishedTemplate(record.ReturnCode, ct);
        var fields = template.CurrentVersion.Fields;
        
        foreach (var row in record.Rows)
        {
            foreach (var field in fields)
            {
                var value = row.GetValue(field.FieldName);
                
                // Required check
                if (field.IsRequired && value == null)
                {
                    errors.Add(new ValidationError
                    {
                        RuleId = $"REQUIRED_{field.FieldName.ToUpper()}",
                        Field = field.FieldName,
                        Message = $"Required field '{field.DisplayName}' is missing",
                        Severity = ValidationSeverity.Error,
                        Category = ValidationCategory.TypeRange
                    });
                    continue;
                }
                
                if (value == null) continue;
                
                // Range checks for numeric fields
                if (field.DataType is FieldDataType.Money or FieldDataType.Decimal or FieldDataType.Integer)
                {
                    var numVal = Convert.ToDecimal(value);
                    
                    if (!string.IsNullOrEmpty(field.MinValue) &&
                        numVal < decimal.Parse(field.MinValue))
                    {
                        errors.Add(new ValidationError
                        {
                            RuleId = $"RANGE_{field.FieldName.ToUpper()}",
                            Field = field.FieldName,
                            Message = $"'{field.DisplayName}' value {numVal} is below minimum {field.MinValue}",
                            Severity = ValidationSeverity.Error,
                            Category = ValidationCategory.TypeRange,
                            ActualValue = numVal.ToString(),
                            ExpectedValue = $">= {field.MinValue}"
                        });
                    }
                    
                    if (!string.IsNullOrEmpty(field.MaxValue) &&
                        numVal > decimal.Parse(field.MaxValue))
                    {
                        errors.Add(new ValidationError
                        {
                            RuleId = $"RANGE_{field.FieldName.ToUpper()}",
                            Field = field.FieldName,
                            Message = $"'{field.DisplayName}' value {numVal} exceeds maximum {field.MaxValue}",
                            Severity = ValidationSeverity.Error,
                            Category = ValidationCategory.TypeRange,
                            ActualValue = numVal.ToString(),
                            ExpectedValue = $"<= {field.MaxValue}"
                        });
                    }
                }
                
                // Allowed values check
                if (!string.IsNullOrEmpty(field.AllowedValues))
                {
                    var allowed = JsonSerializer.Deserialize<string[]>(field.AllowedValues)!;
                    if (!allowed.Contains(value.ToString(), StringComparer.OrdinalIgnoreCase))
                    {
                        errors.Add(new ValidationError
                        {
                            RuleId = $"ALLOWED_{field.FieldName.ToUpper()}",
                            Field = field.FieldName,
                            Message = $"'{field.DisplayName}' value '{value}' is not in allowed values: {field.AllowedValues}",
                            Severity = ValidationSeverity.Error,
                            Category = ValidationCategory.TypeRange,
                            ActualValue = value.ToString()
                        });
                    }
                }
            }
        }
        
        return errors;
    }
}
```

### C.10 IngestionOrchestrator -- Rewritten for Generic Pipeline

```csharp
// FC.Engine.Application/Services/IngestionOrchestrator.cs
namespace FC.Engine.Application.Services;

public class IngestionOrchestrator
{
    private readonly IGenericXmlParser _xmlParser;
    private readonly IXsdGenerator _xsdGenerator;
    private readonly ValidationOrchestrator _validationOrchestrator;
    private readonly ISubmissionRepository _submissionRepo;
    private readonly IGenericDataRepository _dataRepo;
    private readonly ITemplateMetadataCache _cache;
    
    public async Task<SubmissionResultDto> ProcessSubmission(
        Stream xmlStream,
        string returnCode,
        int institutionId,
        int returnPeriodId,
        CancellationToken ct)
    {
        // 0. Verify template exists and is published
        var template = await _cache.GetPublishedTemplate(returnCode, ct);
        if (template == null)
            throw new InvalidOperationException($"No published template for '{returnCode}'");
        
        // 1. Create submission record
        var submission = Submission.Create(institutionId, returnPeriodId, returnCode);
        submission.SetTemplateVersion(template.CurrentVersion.Id);
        await _submissionRepo.Add(submission, ct);
        
        // 2. XSD validation
        var xsd = await _xsdGenerator.GenerateSchema(returnCode, ct);
        var xsdErrors = ValidateAgainstXsd(xmlStream, xsd);
        if (xsdErrors.Any(e => e.Severity == ValidationSeverity.Error))
        {
            submission.MarkRejected();
            var xsdReport = ValidationReport.Create(submission.Id);
            xsdReport.AddErrors(xsdErrors);
            xsdReport.FinalizeAt(DateTime.UtcNow);
            submission.AttachValidationReport(xsdReport);
            await _submissionRepo.Update(submission, ct);
            return MapResult(submission);
        }
        xmlStream.Position = 0;
        
        // 3. Parse XML using metadata-driven generic parser
        submission.MarkParsing();
        var record = await _xmlParser.Parse(xmlStream, returnCode, ct);
        
        // 4. Run all validation phases
        submission.MarkValidating();
        var report = await _validationOrchestrator.ValidateAll(record, submission, ct);
        
        // 5. Persist or reject
        if (report.IsValid && !report.HasWarnings)
        {
            await _dataRepo.Save(record, submission.Id, ct);
            submission.MarkAccepted();
        }
        else if (!report.HasErrors && report.HasWarnings)
        {
            await _dataRepo.Save(record, submission.Id, ct);
            submission.MarkAcceptedWithWarnings();
        }
        else
        {
            submission.MarkRejected();
        }
        
        submission.AttachValidationReport(report);
        await _submissionRepo.Update(submission, ct);
        
        return MapResult(submission);
    }
}
```

### C.11 TemplateService and TemplateVersioningService

```csharp
// FC.Engine.Application/Services/TemplateService.cs
namespace FC.Engine.Application.Services;

public class TemplateService
{
    private readonly ITemplateRepository _repo;
    private readonly IAuditLogger _audit;
    
    public async Task<TemplateDto> CreateTemplate(CreateTemplateRequest request, string userId, CancellationToken ct)
    {
        var template = new ReturnTemplate
        {
            ReturnCode = request.ReturnCode,
            Name = request.Name,
            Description = request.Description,
            Frequency = request.Frequency,
            StructuralCategory = request.StructuralCategory,
            PhysicalTableName = GenerateTableName(request.ReturnCode),
            XmlRootElement = request.ReturnCode.Replace(" ", ""),
            XmlNamespace = $"urn:cbn:dfis:fc:{request.ReturnCode.Replace(" ", "").ToLower()}",
            CreatedBy = userId
        };
        
        // Create initial Draft version
        var version = template.CreateDraftVersion(userId);
        
        await _repo.Add(template, ct);
        await _audit.Log("ReturnTemplate", template.Id, "Create", null, template, userId);
        
        return MapToDto(template);
    }
    
    public async Task AddField(int templateId, int versionId, AddFieldRequest request,
        string userId, CancellationToken ct)
    {
        var template = await _repo.GetById(templateId, ct);
        var version = template.GetVersion(versionId);
        
        if (version.Status != TemplateStatus.Draft)
            throw new InvalidOperationException("Can only modify Draft versions");
        
        version.AddField(new TemplateField
        {
            FieldName = request.FieldName,
            DisplayName = request.DisplayName,
            XmlElementName = request.XmlElementName ?? ToPascalCase(request.FieldName),
            LineCode = request.LineCode,
            DataType = request.DataType,
            SqlType = MapDataTypeToSqlType(request.DataType, request.MaxLength),
            IsRequired = request.IsRequired,
            SectionName = request.SectionName,
            FieldOrder = request.FieldOrder
        });
        
        await _repo.Update(template, ct);
        await _audit.Log("TemplateField", 0, "Create", null, request, userId);
    }
}

// FC.Engine.Application/Services/TemplateVersioningService.cs
public class TemplateVersioningService
{
    private readonly ITemplateRepository _repo;
    private readonly IDdlEngine _ddlEngine;
    private readonly DdlMigrationExecutor _ddlExecutor;
    private readonly IXsdGenerator _xsdGenerator;
    private readonly ITemplateMetadataCache _cache;
    private readonly IAuditLogger _audit;
    
    /// <summary>
    /// Publish a draft version: generates DDL, executes it, and activates the version.
    /// </summary>
    public async Task<PublishResult> PublishVersion(
        int templateId, int versionId, string userId, CancellationToken ct)
    {
        var template = await _repo.GetById(templateId, ct);
        var version = template.GetVersion(versionId);
        
        if (version.Status != TemplateStatus.Review)
            throw new InvalidOperationException("Only versions in Review status can be published");
        
        // Determine if this is new table or alter
        DdlScript ddlScript;
        var previousVersion = template.GetPreviousPublishedVersion(versionId);
        
        if (previousVersion == null)
        {
            // New template: CREATE TABLE
            ddlScript = _ddlEngine.GenerateCreateTable(template, version);
        }
        else
        {
            // Modified template: ALTER TABLE
            ddlScript = _ddlEngine.GenerateAlterTable(template, previousVersion, version);
        }
        
        // Execute DDL within transaction
        var migrationResult = await _ddlExecutor.Execute(
            template.Id, previousVersion?.VersionNumber, version.VersionNumber,
            ddlScript, userId, ct);
        
        if (!migrationResult.Success)
            return new PublishResult(false, migrationResult.Error);
        
        // Update version status
        version.Publish(DateTime.UtcNow, userId);
        version.SetDdlScript(ddlScript.ForwardSql, ddlScript.RollbackSql);
        
        // Deprecate previous version
        previousVersion?.Deprecate();
        
        await _repo.Update(template, ct);
        
        // Invalidate caches
        _cache.Invalidate(template.ReturnCode);
        _xsdGenerator.InvalidateCache(template.ReturnCode);
        
        await _audit.Log("TemplateVersion", versionId, "Publish", null, version, userId);
        
        return new PublishResult(true, null);
    }
    
    /// <summary>
    /// Create a new draft version by cloning the current published version.
    /// </summary>
    public async Task<TemplateVersionDto> CreateNewDraftVersion(
        int templateId, string userId, CancellationToken ct)
    {
        var template = await _repo.GetById(templateId, ct);
        var currentPublished = template.CurrentVersion;
        
        var newVersion = template.CreateDraftVersion(userId);
        
        // Clone all fields from current published version
        foreach (var field in currentPublished.Fields)
        {
            newVersion.AddField(field.Clone());
        }
        
        // Clone all formulas
        foreach (var formula in currentPublished.IntraSheetFormulas)
        {
            newVersion.AddFormula(formula.Clone());
        }
        
        // Clone item codes
        foreach (var itemCode in currentPublished.ItemCodes)
        {
            newVersion.AddItemCode(itemCode.Clone());
        }
        
        await _repo.Update(template, ct);
        return MapToDto(newVersion);
    }
}
```

### C.12 SeedService -- Seeds Existing 103 Templates From schema.sql

```csharp
// FC.Engine.Application/Services/SeedService.cs
namespace FC.Engine.Application.Services;

/// <summary>
/// Parses the existing schema.sql to extract the 103 template definitions
/// and seeds them into the metadata tables. Run once during initial migration.
/// </summary>
public class SeedService
{
    private readonly ITemplateRepository _repo;
    private readonly IDdlEngine _ddlEngine;
    
    /// <summary>
    /// Analyzes each CREATE TABLE statement in schema.sql, extracts:
    /// - Table name -> return_code, physical_table_name
    /// - Column definitions -> template_fields
    /// - Comment line codes -> field line_code values
    /// - Structural category detection (serial_no -> MultiRow, item_code -> ItemCoded, else FixedRow)
    /// </summary>
    public async Task SeedFromSchema(string schemaSqlPath, CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(schemaSqlPath, ct);
        var tables = ParseCreateTableStatements(sql);
        
        foreach (var tableDef in tables)
        {
            if (IsReferenceTable(tableDef.TableName)) continue; // skip institutions, return_periods, etc.
            
            var returnCode = InferReturnCode(tableDef.TableName);
            var category = DetectStructuralCategory(tableDef.Columns);
            var frequency = InferFrequency(returnCode);
            
            var template = new ReturnTemplate
            {
                ReturnCode = returnCode,
                Name = tableDef.Comment, // from the "TABLE N: MFCR 300 - ..." comment
                Frequency = frequency,
                StructuralCategory = category,
                PhysicalTableName = tableDef.TableName,
                XmlRootElement = returnCode.Replace(" ", ""),
                XmlNamespace = $"urn:cbn:dfis:fc:{returnCode.Replace(" ", "").ToLower()}",
                IsSystemTemplate = true,
                CreatedBy = "SYSTEM_SEED"
            };
            
            var version = template.CreateDraftVersion("SYSTEM_SEED");
            
            foreach (var col in tableDef.Columns)
            {
                if (col.Name is "id" or "submission_id" or "created_at") continue;
                
                version.AddField(new TemplateField
                {
                    FieldName = col.Name,
                    DisplayName = InferDisplayName(col.Name),
                    XmlElementName = ToPascalCase(col.Name),
                    LineCode = col.LineCodeComment,
                    DataType = InferFieldDataType(col.SqlType),
                    SqlType = col.SqlType,
                    IsRequired = col.IsNotNull,
                    IsKeyField = col.Name is "serial_no" or "item_code",
                    FieldOrder = col.OrdinalPosition
                });
            }
            
            // Auto-publish since these are existing tables
            version.Publish(DateTime.UtcNow, "SYSTEM_SEED");
            
            await _repo.Add(template, ct);
        }
    }
    
    private static StructuralCategory DetectStructuralCategory(List<ColumnDef> columns)
    {
        if (columns.Any(c => c.Name == "item_code")) return StructuralCategory.ItemCoded;
        if (columns.Any(c => c.Name == "serial_no")) return StructuralCategory.MultiRow;
        return StructuralCategory.FixedRow;
    }
    
    private static string InferReturnCode(string tableName)
    {
        // mfcr_300 -> "MFCR 300"
        // qfcr_364 -> "QFCR 364"
        // mfcr_306_1 -> "MFCR 306-1"
        // fc_car_1 -> "FC CAR 1"
        // ... pattern matching logic
    }
}
```

### C.13 TemplateMetadataCache

```csharp
// FC.Engine.Infrastructure/Caching/TemplateMetadataCache.cs
namespace FC.Engine.Infrastructure.Caching;

public interface ITemplateMetadataCache
{
    Task<CachedTemplate> GetPublishedTemplate(string returnCode, CancellationToken ct);
    void Invalidate(string returnCode);
    void InvalidateAll();
}

public class TemplateMetadataCache : ITemplateMetadataCache
{
    private readonly ConcurrentDictionary<string, CachedTemplate> _cache = new();
    private readonly ITemplateRepository _repo;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public async Task<CachedTemplate> GetPublishedTemplate(string returnCode, CancellationToken ct)
    {
        var key = returnCode.ToUpperInvariant().Trim();
        
        if (_cache.TryGetValue(key, out var cached))
            return cached;
        
        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(key, out cached))
                return cached;
            
            var template = await _repo.GetPublishedByReturnCode(key, ct);
            if (template == null)
                throw new InvalidOperationException($"No published template for '{returnCode}'");
            
            // Build cached view with eager-loaded fields, formulas, item codes
            cached = new CachedTemplate
            {
                ReturnCode = template.ReturnCode,
                Name = template.Name,
                StructuralCategory = template.StructuralCategory,
                PhysicalTableName = template.PhysicalTableName,
                XmlRootElement = template.XmlRootElement,
                XmlNamespace = template.XmlNamespace,
                CurrentVersion = BuildCachedVersion(template.CurrentPublishedVersion!)
            };
            
            _cache[key] = cached;
            return cached;
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public void Invalidate(string returnCode) =>
        _cache.TryRemove(returnCode.ToUpperInvariant().Trim(), out _);
    
    public void InvalidateAll() => _cache.Clear();
}
```

---

## D. Data Flows

### D.1 CBN User Creates a New Template via Admin Portal

```
1. User navigates to Admin Portal -> Templates -> "Create New Template"
2. Fills in: Return Code = "MFCR 2000", Name = "Digital Banking Returns",
   Frequency = Monthly, Category = FixedRow
3. System creates meta.return_templates row + meta.template_versions (v1, status=Draft)
4. User switches to Field Editor tab, adds fields one by one:
   - field_name=digital_wallet_balance, display_name="Digital Wallet Balance",
     data_type=Money, line_code=50110
   - field_name=mobile_transactions_count, display_name="Mobile Transaction Count",
     data_type=Integer, line_code=50120
   - (... more fields ...)
5. Each field insert creates a meta.template_fields row
6. User switches to Formula Builder tab:
   - Creates formula: total_digital_balance = digital_wallet_balance + mobile_money_balance
   - System stores in meta.intra_sheet_formulas:
     rule_code="MFCR2000_SUM_TOTAL_DIGITAL", formula_type=Sum,
     target_field_name="total_digital_balance",
     operand_fields=["digital_wallet_balance","mobile_money_balance"]
7. User clicks "Submit for Review" -> status changes to Review
8. Approver reviews and clicks "Publish"
9. TemplateVersioningService.PublishVersion() executes:
   a. DdlEngine.GenerateCreateTable() produces:
      CREATE TABLE dbo.[mfcr_2000] (
          id INT IDENTITY(1,1) PRIMARY KEY,
          submission_id INT NOT NULL REFERENCES dbo.return_submissions(id),
          digital_wallet_balance DECIMAL(20,2),
          mobile_transactions_count INT,
          ...
          created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
      );
   b. DdlMigrationExecutor runs DDL in a transaction
   c. Records migration in meta.ddl_migrations
   d. Version status -> Published
   e. Cache invalidated
10. Template is now live -- Finance Companies can submit XML for "MFCR 2000"
```

### D.2 Finance Company Submits XML for That Template

```
1. FC submits POST /api/submissions/MFCR%202000 with XML body:
   <MFCR2000 xmlns="urn:cbn:dfis:fc:mfcr2000">
     <Header>
       <InstitutionCode>FC001</InstitutionCode>
       <ReportingDate>2026-01-31</ReportingDate>
       <ReturnCode>MFCR2000</ReturnCode>
     </Header>
     <Data>
       <DigitalWalletBalance>5000000.00</DigitalWalletBalance>
       <MobileTransactionsCount>15432</MobileTransactionsCount>
       ...
     </Data>
   </MFCR2000>

2. IngestionOrchestrator.ProcessSubmission():
   a. Looks up template metadata from cache (CachedTemplate for "MFCR 2000")
   b. Creates Submission record (status=Draft)
   
3. XSD Validation:
   a. XsdGenerator.GenerateSchema("MFCR 2000") reads template_fields from cache
   b. Generates XSD on-the-fly (cached after first generation)
   c. Validates XML against generated XSD
   d. If XSD errors: Submission -> Rejected, return validation report
   
4. XML Parsing:
   a. GenericXmlParser.Parse(xmlStream, "MFCR 2000")
   b. Reads field definitions from cache: [{FieldName:"digital_wallet_balance", XmlElementName:"DigitalWalletBalance", DataType:Money}, ...]
   c. For each field, finds <DigitalWalletBalance> element, converts to decimal
   d. Returns ReturnDataRecord with one ReturnDataRow containing all field values
   
5. Validation:
   a. ValidationOrchestrator.ValidateAll():
      Phase 1 - TypeRange: checks required fields, min/max, allowed_values
      Phase 2 - IntraSheet: FormulaEvaluator reads formula metadata:
        - MFCR2000_SUM_TOTAL_DIGITAL: Sum type, target=total_digital_balance,
          operands=[digital_wallet_balance, mobile_money_balance]
        - Gets operand values from ReturnDataRow, computes sum, compares to target
      Phase 3 - CrossSheet: (none defined for new template initially)
      Phase 4 - Business: (none defined initially)
   b. Returns ValidationReport with any errors/warnings
   
6. Persistence:
   a. If valid: GenericDataRepository.Save(record, submissionId)
   b. DynamicSqlBuilder.BuildInsert() generates:
      INSERT INTO dbo.[mfcr_2000] (submission_id, digital_wallet_balance, mobile_transactions_count, ...)
      VALUES (@submission_id, @digital_wallet_balance, @mobile_transactions_count, ...)
   c. Executes via Dapper with parameterized values
   d. Submission -> Accepted
   
7. Returns SubmissionResultDto with validation report
```

### D.3 CBN User Modifies an Existing Template (Adds a Field)

```
1. User navigates to Admin Portal -> Templates -> "MFCR 300" -> "Create New Version"
2. TemplateVersioningService.CreateNewDraftVersion():
   a. Clones current published version (v1) into new draft (v2)
   b. Copies all 40+ fields, all formulas, all item codes
   c. v2 status = Draft
   
3. User adds new field in Field Editor:
   - field_name=digital_assets, display_name="Digital Assets (Crypto)",
     data_type=Money, line_code=10675
   
4. User modifies existing formula for total_assets:
   - Adds "digital_assets" to the operand list of the total_assets sum formula
   
5. User clicks "Preview":
   - System generates sample XSD showing new field
   - Shows impact analysis: "42 institutions will need to include this field"
   
6. User submits for Review, Approver publishes
7. TemplateVersioningService.PublishVersion():
   a. DdlEngine.GenerateAlterTable(template, v1, v2):
      - Compares fields: v2 has one new field "digital_assets"
      - Generates: ALTER TABLE dbo.[mfcr_300] ADD [digital_assets] DECIMAL(20,2);
   b. Executes DDL
   c. Records migration: version_from=1, version_to=2, migration_type=AddColumn
   d. v1 -> Deprecated, v2 -> Published
   e. Cache invalidated
   
8. Existing data in mfcr_300 is preserved -- new column has NULL for old submissions
9. New submissions will include the digital_assets field
10. Formulas automatically pick up the new field since they reference by field_name
```

---

## E. Admin Portal Pages (Blazor Server)

### E.1 Dashboard (`/admin`)
- Overview statistics: 103 templates, 508 formulas, recent submissions
- Pending approvals: draft versions awaiting review
- Recent audit activity
- System health: last DDL migration, cache status

### E.2 Template List (`/admin/templates`)
- Searchable/filterable data grid of all 103+ templates
- Columns: Return Code, Name, Frequency, Category, Status, Version, Last Modified
- Filters: by frequency (Monthly/Quarterly/Semi-Annual), by category, by status
- Actions: View, Create New, Create New Version

### E.3 Template Designer (`/admin/templates/{id}`)
- **Header section**: Return Code, Name, Description, Frequency, Category (read-only after creation)
- **Tabs**:
  - **Fields**: Editable data grid with drag-and-drop reordering
    - Columns: Field Name, Display Name, XML Element, Line Code, Data Type, Required, Section
    - Add/Remove/Edit fields (only if version is Draft)
    - Bulk import from CSV
  - **Sections**: Define section groupings with ordering
  - **Item Codes** (visible only for ItemCoded): Define predefined item codes and descriptions
  - **Preview**: Shows auto-generated XSD and sample XML
  - **History**: Version timeline with diff view

### E.4 Formula Builder (`/admin/templates/{id}/formulas`)
- List of all intra-sheet formulas for this template
- Inline formula editor:
  - Target field dropdown (auto-populated from template fields)
  - Formula type selector (Sum, Difference, Equals, Custom, etc.)
  - Operand field multi-select
  - Custom expression editor (for Formula type = Custom)
  - Tolerance settings (amount or percentage)
  - Severity selector
  - Custom error message
- **Test Runner**: Upload sample XML and run formulas to see validation results

### E.5 Cross-Sheet Rule Editor (`/admin/cross-sheet-rules`)
- List of all 45 cross-sheet rules
- Editor:
  - Rule code, name, description
  - Operand grid: Alias (A, B, C) + Template dropdown + Field dropdown + Aggregate function
  - Expression builder: "A = B + C" with syntax highlighting
  - Tolerance settings
- Test with sample data from specific institution/period

### E.6 Business Rule Editor (`/admin/business-rules`)
- List of business rules
- Expression editor with template/field references
- Applicability: which templates/fields this rule applies to

### E.7 Version History & Approval (`/admin/templates/{id}/versions`)
- Timeline of all versions with status badges (Draft, Review, Published, Deprecated)
- Diff view: compare two versions side by side (fields added/removed/changed)
- Approval workflow: Review button, Publish button (with confirmation)
- Rollback capability: revert to previous version (re-executes rollback DDL)

### E.8 Submission Browser (`/admin/submissions`)
- Data grid of all submissions across all templates
- Filters: by institution, by template, by period, by status
- Drill-down to validation report
- Reprocessing: re-validate existing submissions against updated formulas

### E.9 Audit Log (`/admin/audit`)
- Chronological log of all metadata changes
- Filters: by entity type, by user, by date range
- Detail view: shows old/new values as JSON diff

### E.10 Impact Analysis (`/admin/templates/{id}/impact`)
- When modifying a template: shows which institutions have existing submissions
- When changing formulas: shows how many existing submissions would pass/fail
- Dry-run: re-validate all existing data against proposed changes without persisting

---

## F. Phased Implementation Plan

### Phase 1: Foundation (Weeks 1-4)
**Goal**: Metadata schema, core engine, seed existing 103 templates

1. **Week 1**: Metadata database schema
   - Create all `meta.*` tables (return_templates, template_versions, template_fields, etc.)
   - Create validation rule tables (intra_sheet_formulas, cross_sheet_rules, business_rules)
   - Create audit_log and ddl_migrations tables
   - EF Core migrations for metadata schema

2. **Week 2**: Domain model and metadata repository
   - `ReturnDataRecord` / `ReturnDataRow` (replaces all IReturnData implementations)
   - `ReturnTemplate`, `TemplateVersion`, `TemplateField` domain entities
   - `IntraSheetFormula`, `CrossSheetRule`, `BusinessRule` entities
   - `ITemplateRepository`, `IFormulaRepository` with EF implementations
   - `TemplateMetadataCache` with in-memory caching

3. **Week 3**: DdlEngine and SeedService
   - `DdlEngine`: GenerateCreateTable, GenerateAlterTable
   - `DdlMigrationExecutor`: safe DDL execution with transaction and logging
   - `SeedService`: parse schema.sql, create metadata rows for all 103 templates
   - Execute seed: all 103 templates in metadata with Published status

4. **Week 4**: Testing Phase 1
   - Unit tests for DdlEngine (generate expected DDL from metadata)
   - Unit tests for SeedService (verify all 103 templates correctly parsed)
   - Integration test: seed -> verify metadata -> verify physical tables exist

### Phase 2: Generic Pipeline (Weeks 5-8)
**Goal**: Replace hardcoded parsing and persistence for all templates

5. **Week 5**: XsdGenerator and GenericXmlParser
   - `XsdGenerator`: generate XSD from template_fields metadata (FixedRow, MultiRow, ItemCoded)
   - `GenericXmlParser`: parse any XML using field metadata
   - Verify: generated XSD for MFCR 300 matches existing `MFCR300.xsd` semantically
   - Verify: GenericXmlParser produces same data as Mfcr300XmlParser

6. **Week 6**: GenericDataRepository
   - `DynamicSqlBuilder`: parameterized INSERT/SELECT/DELETE
   - `GenericDataRepository`: Dapper-based CRUD against any physical table
   - Verify: roundtrip test -- parse XML, save via generic repo, read back, compare

7. **Week 7**: Rewrite IngestionOrchestrator
   - Replace typed pipeline with generic pipeline
   - Remove `IReturnData`, `Mfcr300Data`, `Mfcr300XmlParser`, `Mfcr300Entity`, etc.
   - Remove `XmlParserFactory`, `XsdSchemaProvider` (replaced by metadata-driven versions)
   - Remove all per-template DI registrations

8. **Week 8**: Testing Phase 2
   - End-to-end: submit XML for each of the 3 structural categories
   - Regression: submit MFCR 300 XML through generic pipeline, compare results to old
   - Performance: measure latency of metadata lookup + generic parsing + Dapper persistence

### Phase 3: Validation Engine (Weeks 9-12)
**Goal**: Formula evaluator, cross-sheet validator, expression parser

9. **Week 9**: FormulaEvaluator
   - Implement all formula types: Sum, Difference, Equals, GreaterThan, etc.
   - Tolerance handling (amount and percentage)
   - Seed MFCR 300 sum rules into metadata from existing `Mfcr300SumRules.cs`
   - Verify: FormulaEvaluator produces identical errors to hardcoded Mfcr300SumRules

10. **Week 10**: ExpressionParser
    - Tokenizer and shunting-yard parser for arithmetic expressions
    - Comparison expression evaluation
    - Unit tests: "A + B - C", "A >= B * 0.125", "(A + B) / C = D"

11. **Week 11**: CrossSheetValidator and BusinessRuleEvaluator
    - Implement cross-sheet rule loading and evaluation
    - Seed the 45 cross-sheet rules (XS-001 through XS-045)
    - BusinessRuleEvaluator for generic rules

12. **Week 12**: Seed All 508 Formulas
    - Parse formula_catalog.xlsx into intra_sheet_formulas rows
    - Create seed script for cross-sheet rules
    - Full validation regression test against sample data

### Phase 4: Admin Portal (Weeks 13-18)
**Goal**: Blazor Server admin portal for template and rule management

13. **Week 13**: Admin portal scaffolding
    - Blazor Server project setup with authentication
    - Shared layout, navigation, common components
    - Dashboard page

14. **Week 14**: Template management pages
    - Template list with search/filter
    - Template designer: field editor grid
    - Data type selector, section management

15. **Week 15**: Formula builder
    - Intra-sheet formula editor with field dropdowns
    - Formula type selector with dynamic operand configuration
    - Custom expression editor

16. **Week 16**: Cross-sheet rules and business rules
    - Cross-sheet rule editor with multi-template operands
    - Expression builder with syntax validation
    - Business rule editor

17. **Week 17**: Versioning and publishing workflow
    - Version history timeline
    - Diff view (field-level comparison between versions)
    - Publish workflow with DDL preview and confirmation
    - Rollback capability

18. **Week 18**: Testing and polish
    - End-to-end: create template in Admin -> submit XML via API -> view results in Admin
    - Audit log page
    - Impact analysis
    - Submission browser

### Phase 5: Production Hardening (Weeks 19-22)
**Goal**: Performance, security, documentation, deployment

19. **Week 19**: Performance optimization
    - Template metadata cache warming on startup
    - XSD cache management
    - Dapper query optimization
    - Connection pooling configuration

20. **Week 20**: Security and auth
    - Role-based access: Admin (create/edit), Approver (publish), Viewer (read-only)
    - API authentication (JWT or API keys for FC submissions)
    - Admin portal authentication (CBN internal auth)
    - SQL injection prevention review (parameterized queries throughout)

21. **Week 21**: Docker Compose update and deployment
    - Add Blazor Admin service to docker-compose.yml
    - Health checks for all services
    - Environment-specific configuration
    - CI/CD pipeline

22. **Week 22**: Documentation and handover
    - Admin portal user guide
    - API documentation (OpenAPI/Swagger)
    - Template creation guide for CBN users
    - Formula authoring guide
    - Operations runbook

---

## G. Key Design Decisions

### G.1 Dictionary<string, object?> vs Strongly-Typed

**Decision**: Use `ReturnDataRecord` containing `ReturnDataRow` (which wraps `Dictionary<string, object?>`) as the universal data container.

**Rationale**: The entire point of the metadata-driven approach is that template structure is defined in the database, not in code. Strongly-typed C# classes per template are exactly what we are eliminating. The dictionary approach:
- Works identically for all 103 templates without code changes
- New templates added via Admin Portal work immediately
- Field additions require no code deployment
- Type safety is enforced at the metadata level (FieldDataType enum + runtime conversion)
- Performance penalty of dictionary lookup vs property access is negligible (nanoseconds) compared to DB I/O

**Guard rails**: The `ReturnDataRow.GetDecimal()`, `GetValue()` etc. methods provide type-safe accessors. The `FieldDataType` enum in metadata ensures correct conversion at parse time.

### G.2 How to Generate DDL Safely

**Decision**: DdlEngine generates DDL scripts. DdlMigrationExecutor runs them in a transaction with pre/post verification.

**Safety measures**:
1. DDL is generated, not hand-written -- eliminates SQL injection risk in table/column names (validated against regex `^[a-z_][a-z0-9_]*$`)
2. ALTER TABLE only adds columns or widens types -- never drops columns (data preservation)
3. Every DDL execution is recorded in `meta.ddl_migrations` with a rollback script
4. Publish workflow requires Review status -- two-person approval
5. Preview mode: admin can see the exact DDL before execution
6. DDL runs in a serializable transaction with schema lock
7. Table names use a safe prefix pattern derived from return codes

### G.3 Template Version Migrations (Data in Old Table Format)

**Decision**: When a template version changes, the physical table is altered in place. Old data rows retain their existing column values. New columns are nullable and contain NULL for pre-existing rows.

**Key principles**:
- Columns are never dropped (deprecated fields stay in the table but are removed from the active template version's field list)
- Column types can only be widened (e.g., `VARCHAR(100)` to `VARCHAR(255)`, `DECIMAL(20,2)` to `DECIMAL(30,2)`)
- Each submission records `template_version_id`, so the system knows which version's fields apply
- When reading old data, missing fields (not in old version) return NULL
- When re-validating old data, the system uses the version-at-time-of-submission, not the current version

### G.4 Expression Language for Custom Formulas

**Decision**: Simple arithmetic expression language with standard operator precedence.

**Supported syntax**:
```
Arithmetic: A + B - C * D / E
Grouping:   (A + B) * C
Comparison: A = B + C
             A >= B * 0.125
             A != 0
             A > B AND C > D    (future: boolean connectives)
Variables:  field_name (e.g., total_assets)
            alias (e.g., A, B, C -- used in cross-sheet rules)
Literals:   123.45, 0.125
```

**Implementation**: Standard shunting-yard algorithm (Dijkstra) with a tokenizer and postfix evaluator. This is approximately 200 lines of code and handles all common financial formula patterns without requiring an external expression library.

**Why not use a full scripting engine (Roslyn, NCalc, etc.)?**: Security. CBN business users will author these expressions through a UI. A simple arithmetic-only parser prevents injection of arbitrary code. NCalc would be a reasonable alternative if richer expressions are needed in the future.

### G.5 How the Generic XML Parser Works Without Per-Template Code

**Decision**: The parser reads template field metadata (cached) and iterates over the XML elements to extract values by matching `xml_element_name` to actual XML element names.

**Mechanism**:
1. Template metadata (from cache) provides the list of expected fields with their `xml_element_name` and `data_type`
2. The `structural_category` (FixedRow/MultiRow/ItemCoded) determines the XML traversal strategy
3. For **FixedRow**: navigate to the data section, iterate fields, find matching elements
4. For **MultiRow**: find repeating Row elements, for each row iterate fields
5. For **ItemCoded**: find repeating Item elements, identify by item_code field, iterate remaining fields

**The parser is truly generic** -- it uses zero template-specific code. Adding a new template with 50 fields requires only 50 rows in `meta.template_fields`, zero C# code.

### G.6 How the Generic Data Repository Does CRUD Without EF Entity Classes

**Decision**: Use Dapper with dynamically constructed parameterized SQL.

**Mechanism**:
1. `DynamicSqlBuilder.BuildInsert()` reads the field list from metadata and the values from `ReturnDataRow`
2. It constructs: `INSERT INTO dbo.[{tableName}] ([field1], [field2], ...) VALUES (@field1, @field2, ...)`
3. All values are bound as Dapper `DynamicParameters` -- fully parameterized, no SQL injection
4. `BuildSelect()` similarly constructs: `SELECT [field1], [field2], ... FROM dbo.[{tableName}] WHERE submission_id = @submissionId`
5. Dapper returns `IEnumerable<dynamic>` which is cast to `IDictionary<string, object>` for field extraction

**Why Dapper over EF Core for data tables?**: EF Core requires compile-time entity classes. Since template structure is defined at runtime in metadata, EF Core's strongly-typed model is not applicable for the 103 data tables. EF Core IS still used for the operational tables (submissions, institutions, etc.) and the metadata tables (return_templates, template_fields, etc.) which have fixed schemas.

---

### Critical Files for Implementation
- `/Users/mac/codes/fcs/schema.sql` - Source of truth for all 103 existing table structures, used by SeedService to populate metadata
- `/Users/mac/codes/fcs/FC Engine/src/FC.Engine.Infrastructure/Validation/Rules/IntraSheet/Mfcr300SumRules.cs` - Pattern for converting hardcoded validation rules into metadata-driven IntraSheetFormula rows
- `/Users/mac/codes/fcs/FC Engine/src/FC.Engine.Application/Services/IngestionOrchestrator.cs` - The submission pipeline to be rewritten to use generic metadata-driven components
- `/Users/mac/codes/fcs/FC Engine/src/FC.Engine.Infrastructure/Xml/Parsers/Mfcr300XmlParser.cs` - Reference for how XML parsing currently works per-template, to be replaced by GenericXmlParser
- `/Users/mac/codes/fcs/FC Engine/src/FC.Engine.Infrastructure/Persistence/Repositories/ReturnRepository.cs` - The per-template switch/case persistence to be replaced by GenericDataRepository with Dapper