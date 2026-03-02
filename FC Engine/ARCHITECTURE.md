# Comprehensive Implementation Plan: CBN DFIS FC Returns Data Processing Engine

## 1. Solution Architecture Overview

### 1.1 High-Level Data Flow

```
Finance Company                FC Engine API               SQL Server
     |                             |                          |
     |  XML Submission             |                          |
     |  (per return code)          |                          |
     |---------------------------->|                          |
     |                             |                          |
     |                    [1. Receive XML]                    |
     |                    [2. XSD Validation]                 |
     |                    [3. Parse to Domain]                |
     |                    [4. Type/Range Validate]            |
     |                    [5. Intra-sheet Validate]           |
     |                    [6. Cross-sheet Validate]           |
     |                    [7. Business Rules]                 |
     |                    [8. Persist if valid]               |
     |                             |                          |
     |                             |-------persist----------->|
     |                             |                          |
     |<---Validation Report--------|                          |
     |                             |                          |
```

### 1.2 Submission Lifecycle State Machine

```
UPLOAD --> PARSING --> VALIDATING --> [VALID] --> ACCEPTED --> STORED
                         |
                         +--> [INVALID] --> REJECTED (with report)
                         |
                         +--> [WARNINGS] --> ACCEPTED_WITH_WARNINGS --> STORED
```

### 1.3 Clean Architecture Layers

```
+-----------------------------------------------------------------+
|  FC.Engine.Api  (ASP.NET 8 Minimal API / Controllers)           |
|  - Endpoints: /submissions, /returns/{code}, /validation-report |
|  - Middleware: Auth, Error handling, Request logging             |
+-----------------------------------------------------------------+
         |  depends on
         v
+-----------------------------------------------------------------+
|  FC.Engine.Application  (Use Cases / CQRS Handlers)             |
|  - Commands: SubmitReturn, ValidateReturn, ResubmitReturn       |
|  - Queries: GetSubmission, GetValidationReport, GetReturnData   |
|  - Services: IngestionOrchestrator, ValidationOrchestrator      |
+-----------------------------------------------------------------+
         |  depends on
         v
+-----------------------------------------------------------------+
|  FC.Engine.Domain  (Entities, Value Objects, Rules)              |
|  - Entities: Submission, ReturnData, ValidationResult            |
|  - Value Objects: ReturnCode, ReportingPeriod, MoneyAmount      |
|  - Interfaces: IReturnValidator, IReturnRepository              |
|  - Validation Rules: FormulaRule, CrossSheetRule, BusinessRule   |
+-----------------------------------------------------------------+
         ^
         |  implemented by
+-----------------------------------------------------------------+
|  FC.Engine.Infrastructure  (EF Core, XML, SQL Server)           |
|  - Persistence: DbContext, Repositories, Migrations             |
|  - Xml: XsdSchemaProvider, XmlParser, ReturnDeserializers       |
|  - Validation: RuleEngine, FormulaEvaluator                     |
+-----------------------------------------------------------------+
         ^
         |  configuration / hosting
+-----------------------------------------------------------------+
|  FC.Engine.Migrator  (standalone tool for DB migrations)         |
+-----------------------------------------------------------------+
```

---

## 2. Solution Structure

### 2.1 Directory Tree

```
/Users/mac/codes/fcs/FC Engine/
|
+-- FCEngine.sln
|
+-- src/
|   +-- FC.Engine.Domain/
|   |   +-- FC.Engine.Domain.csproj
|   |   +-- Entities/
|   |   |   +-- Submission.cs
|   |   |   +-- ReturnHeader.cs
|   |   |   +-- Institution.cs
|   |   |   +-- ReturnPeriod.cs
|   |   |   +-- ValidationReport.cs
|   |   |   +-- ValidationError.cs
|   |   +-- ValueObjects/
|   |   |   +-- ReturnCode.cs
|   |   |   +-- ReportingPeriod.cs
|   |   |   +-- MoneyAmount.cs
|   |   |   +-- LineCode.cs
|   |   +-- Enums/
|   |   |   +-- SubmissionStatus.cs
|   |   |   +-- ReturnFrequency.cs
|   |   |   +-- ValidationSeverity.cs
|   |   |   +-- ValidationCategory.cs
|   |   +-- Returns/
|   |   |   +-- IReturnData.cs            (marker interface for all return data)
|   |   |   +-- FixedRow/
|   |   |   |   +-- Mfcr300Data.cs        (Statement of Financial Position)
|   |   |   |   +-- Mfcr1000Data.cs       (Comprehensive Income)
|   |   |   |   +-- Mfcr100Data.cs        (Memorandum Items)
|   |   |   |   +-- FcCar2Data.cs         (Capital Adequacy)
|   |   |   |   +-- FcAcrData.cs          (Adjusted Capital Ratio)
|   |   |   |   +-- ... (other single-record returns)
|   |   |   +-- MultiRow/
|   |   |   |   +-- Mfcr302Row.cs         (Balances Held with Banks)
|   |   |   |   +-- Mfcr304Row.cs         (Secured Money at Call)
|   |   |   |   +-- Mfcr318Row.cs         (Treasury Bills)
|   |   |   |   +-- Mfcr349Row.cs         (Loans - Other Customers)
|   |   |   |   +-- Qfcr364Row.cs         (Direct Credit Substitutes)
|   |   |   |   +-- ... (other list returns)
|   |   |   +-- ItemCoded/
|   |   |       +-- Mfcr356Item.cs        (Other Assets - keyed by item_code)
|   |   |       +-- Mfcr1002Item.cs       (Govt Securities Income)
|   |   |       +-- Mfcr1540Item.cs       (Maturity Profile)
|   |   |       +-- Mfcr1550Item.cs       (Sectoral Credit)
|   |   |       +-- FcCar1Item.cs         (Risk-Weighted Assets)
|   |   |       +-- ... (other item-coded returns)
|   |   +-- Validation/
|   |   |   +-- IValidationRule.cs
|   |   |   +-- IIntraSheetRule.cs
|   |   |   +-- ICrossSheetRule.cs
|   |   |   +-- IBusinessRule.cs
|   |   |   +-- ValidationContext.cs
|   |   |   +-- FormulaExpression.cs
|   |   +-- Abstractions/
|   |       +-- IReturnRepository.cs
|   |       +-- ISubmissionRepository.cs
|   |       +-- IXmlParser.cs
|   |       +-- IValidationEngine.cs
|   |       +-- IRuleRegistry.cs
|   |
|   +-- FC.Engine.Application/
|   |   +-- FC.Engine.Application.csproj
|   |   +-- DependencyInjection.cs
|   |   +-- Commands/
|   |   |   +-- SubmitReturn/
|   |   |   |   +-- SubmitReturnCommand.cs
|   |   |   |   +-- SubmitReturnHandler.cs
|   |   |   +-- ValidateReturn/
|   |   |   |   +-- ValidateReturnCommand.cs
|   |   |   |   +-- ValidateReturnHandler.cs
|   |   |   +-- SubmitBatch/
|   |   |       +-- SubmitBatchCommand.cs
|   |   |       +-- SubmitBatchHandler.cs
|   |   +-- Queries/
|   |   |   +-- GetSubmission/
|   |   |   |   +-- GetSubmissionQuery.cs
|   |   |   |   +-- GetSubmissionHandler.cs
|   |   |   +-- GetValidationReport/
|   |   |   |   +-- GetValidationReportQuery.cs
|   |   |   |   +-- GetValidationReportHandler.cs
|   |   |   +-- GetReturnData/
|   |   |       +-- GetReturnDataQuery.cs
|   |   |       +-- GetReturnDataHandler.cs
|   |   +-- Services/
|   |   |   +-- IngestionOrchestrator.cs
|   |   |   +-- ValidationOrchestrator.cs
|   |   |   +-- CrossSheetValidationService.cs
|   |   +-- DTOs/
|   |       +-- SubmissionDto.cs
|   |       +-- ValidationReportDto.cs
|   |       +-- ReturnDataDto.cs
|   |
|   +-- FC.Engine.Infrastructure/
|   |   +-- FC.Engine.Infrastructure.csproj
|   |   +-- DependencyInjection.cs
|   |   +-- Persistence/
|   |   |   +-- FcEngineDbContext.cs
|   |   |   +-- Configurations/
|   |   |   |   +-- SubmissionConfiguration.cs
|   |   |   |   +-- InstitutionConfiguration.cs
|   |   |   |   +-- Mfcr300Configuration.cs
|   |   |   |   +-- Mfcr1000Configuration.cs
|   |   |   |   +-- ... (one per table)
|   |   |   +-- Repositories/
|   |   |   |   +-- SubmissionRepository.cs
|   |   |   |   +-- ReturnRepository.cs
|   |   |   +-- Migrations/
|   |   |       +-- (EF Core generated)
|   |   +-- Xml/
|   |   |   +-- Schemas/
|   |   |   |   +-- MFCR300.xsd
|   |   |   |   +-- MFCR1000.xsd
|   |   |   |   +-- MFCR100.xsd
|   |   |   |   +-- MFCR302.xsd
|   |   |   |   +-- ... (103 XSD files)
|   |   |   +-- XsdSchemaProvider.cs
|   |   |   +-- XmlReturnParser.cs
|   |   |   +-- Parsers/
|   |   |   |   +-- IReturnXmlParser.cs
|   |   |   |   +-- Mfcr300XmlParser.cs
|   |   |   |   +-- Mfcr1000XmlParser.cs
|   |   |   |   +-- Mfcr302XmlParser.cs
|   |   |   |   +-- ... (one per return type)
|   |   |   +-- XmlParserFactory.cs
|   |   +-- Validation/
|   |       +-- RuleEngine.cs
|   |       +-- RuleRegistry.cs
|   |       +-- FormulaEvaluator.cs
|   |       +-- Rules/
|   |       |   +-- IntraSheet/
|   |       |   |   +-- Mfcr300SumRules.cs
|   |       |   |   +-- Mfcr1000ComputedRules.cs
|   |       |   |   +-- Mfcr356RowTotalRules.cs
|   |       |   |   +-- ... (one per return with formulas)
|   |       |   +-- CrossSheet/
|   |       |   |   +-- Mfcr302ToMfcr300Rule.cs
|   |       |   |   +-- Mfcr304ToMfcr300Rule.cs
|   |       |   |   +-- Mfcr318ToMfcr300Rule.cs
|   |       |   |   +-- Mfcr1000ToMfcr1002Rule.cs
|   |       |   |   +-- Mfcr387ToMfcr1000Rule.cs
|   |       |   |   +-- FcCarToMfcr300Rule.cs
|   |       |   |   +-- FcAcrToMfcr300And1000Rule.cs
|   |       |   |   +-- ... (one per known cross-reference)
|   |       |   +-- Business/
|   |       |       +-- DateInPastRule.cs
|   |       |       +-- DecimalPrecisionRule.cs
|   |       |       +-- NonZeroRequiredFieldRule.cs
|   |       |       +-- DropdownValueRule.cs
|   |       |       +-- ConditionalRequiredRule.cs
|   |       +-- RuleDefinitions/
|   |           +-- formula_rules.json     (machine-readable formula catalog)
|   |           +-- crosssheet_rules.json
|   |           +-- business_rules.json
|   |
|   +-- FC.Engine.Api/
|   |   +-- FC.Engine.Api.csproj
|   |   +-- Program.cs
|   |   +-- appsettings.json
|   |   +-- appsettings.Development.json
|   |   +-- Endpoints/
|   |   |   +-- SubmissionEndpoints.cs
|   |   |   +-- ValidationEndpoints.cs
|   |   |   +-- ReturnDataEndpoints.cs
|   |   |   +-- ReferenceDataEndpoints.cs
|   |   |   +-- SchemaEndpoints.cs
|   |   +-- Middleware/
|   |   |   +-- ExceptionHandlingMiddleware.cs
|   |   |   +-- RequestLoggingMiddleware.cs
|   |   +-- Filters/
|   |       +-- ValidationFilter.cs
|   |
|   +-- FC.Engine.Migrator/
|       +-- FC.Engine.Migrator.csproj
|       +-- Program.cs
|
+-- tests/
|   +-- FC.Engine.Domain.Tests/
|   |   +-- FC.Engine.Domain.Tests.csproj
|   |   +-- ValueObjects/
|   |   +-- Validation/
|   +-- FC.Engine.Application.Tests/
|   |   +-- FC.Engine.Application.Tests.csproj
|   |   +-- Commands/
|   |   +-- Services/
|   +-- FC.Engine.Infrastructure.Tests/
|   |   +-- FC.Engine.Infrastructure.Tests.csproj
|   |   +-- Xml/
|   |   +-- Validation/
|   +-- FC.Engine.Integration.Tests/
|       +-- FC.Engine.Integration.Tests.csproj
|       +-- SubmissionPipelineTests.cs
|       +-- CrossSheetValidationTests.cs
|
+-- docker/
|   +-- docker-compose.yml
|   +-- docker-compose.override.yml
|   +-- Dockerfile.api
|   +-- Dockerfile.migrator
|   +-- .env.example
|
+-- scripts/
|   +-- seed-reference-data.sql
|   +-- generate-xsd.ps1         (PowerShell script to help generate XSDs)
|
+-- docs/
    +-- return-codes.md
    +-- validation-rules.md
    +-- api-reference.md
    +-- cross-sheet-map.md
```

---

## 3. Key Classes and Interfaces

### 3.1 Domain Layer -- Core Entities

**`Submission.cs`** -- The aggregate root for a regulatory return submission:

```csharp
namespace FC.Engine.Domain.Entities;

public class Submission
{
    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public int ReturnPeriodId { get; private set; }
    public ReturnCode ReturnCode { get; private set; }
    public SubmissionStatus Status { get; private set; }
    public DateTime SubmissionDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    
    // Navigation
    public Institution Institution { get; private set; }
    public ReturnPeriod ReturnPeriod { get; private set; }
    public ValidationReport? ValidationReport { get; private set; }
    
    public void MarkValidating() => Status = SubmissionStatus.Validating;
    public void MarkAccepted() => Status = SubmissionStatus.Accepted;
    public void MarkRejected() => Status = SubmissionStatus.Rejected;
    public void MarkAcceptedWithWarnings() => Status = SubmissionStatus.AcceptedWithWarnings;
}
```

**`ReturnCode.cs`** -- Value object encapsulating a CBN return code:

```csharp
namespace FC.Engine.Domain.ValueObjects;

public record ReturnCode
{
    public string Value { get; }
    public string Prefix { get; }       // MFCR, QFCR, SFCR, FC
    public int Number { get; }          // 300, 1000, etc.
    public string? Suffix { get; }      // "1" for combined tables like 306-1
    public ReturnFrequency Frequency { get; }
    
    // Factory method parses "MFCR 300", "QFCR 364", "FC CAR 1", etc.
    public static ReturnCode Parse(string code) { ... }
    
    // Maps to the SQL table name: "mfcr_300", "qfcr_364", "fc_car_1"
    public string ToTableName() { ... }
    
    // Maps to the XSD file name: "MFCR300.xsd"
    public string ToXsdFileName() { ... }
}
```

**`ValidationReport.cs`** -- Captures all validation outcomes:

```csharp
namespace FC.Engine.Domain.Entities;

public class ValidationReport
{
    public int Id { get; private set; }
    public int SubmissionId { get; private set; }
    public bool IsValid => !Errors.Any(e => e.Severity == ValidationSeverity.Error);
    public bool HasWarnings => Errors.Any(e => e.Severity == ValidationSeverity.Warning);
    public List<ValidationError> Errors { get; private set; } = new();
    public DateTime ValidatedAt { get; private set; }
    
    public void AddError(string ruleId, string field, string message, 
        ValidationSeverity severity, ValidationCategory category) { ... }
}

public class ValidationError
{
    public string RuleId { get; set; }           // e.g., "MFCR300_SUM_CASH"
    public string Field { get; set; }            // e.g., "total_cash"
    public string Message { get; set; }          // Human-readable
    public ValidationSeverity Severity { get; set; }  // Error, Warning, Info
    public ValidationCategory Category { get; set; }  // Schema, IntraSheet, CrossSheet, Business
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
    public string? ReferencedReturnCode { get; set; }  // For cross-sheet errors
}
```

### 3.2 Domain Layer -- Validation Abstractions

```csharp
namespace FC.Engine.Domain.Validation;

/// <summary>
/// Base interface for all validation rules.
/// </summary>
public interface IValidationRule
{
    string RuleId { get; }
    ValidationCategory Category { get; }
    ValidationSeverity DefaultSeverity { get; }
}

/// <summary>
/// Validates data within a single return sheet.
/// Receives the parsed domain data for one return code.
/// </summary>
public interface IIntraSheetRule : IValidationRule
{
    ReturnCode ApplicableReturnCode { get; }
    IEnumerable<ValidationError> Validate(IReturnData data);
}

/// <summary>
/// Validates data across multiple return sheets for the same submission period.
/// Receives a context with access to all returns in the submission batch.
/// </summary>
public interface ICrossSheetRule : IValidationRule
{
    IReadOnlyList<ReturnCode> RequiredReturnCodes { get; }
    IEnumerable<ValidationError> Validate(CrossSheetValidationContext context);
}

/// <summary>
/// Generic business rules (date, range, conditional).
/// </summary>
public interface IBusinessRule : IValidationRule
{
    IEnumerable<ValidationError> Validate(IReturnData data, BusinessRuleContext context);
}

/// <summary>
/// Provides access to all returns in a submission batch for cross-validation.
/// </summary>
public class CrossSheetValidationContext
{
    private readonly Dictionary<ReturnCode, IReturnData> _returns = new();
    
    public void Register(ReturnCode code, IReturnData data) => _returns[code] = data;
    public T? GetReturn<T>(ReturnCode code) where T : class, IReturnData 
        => _returns.GetValueOrDefault(code) as T;
    public bool HasReturn(ReturnCode code) => _returns.ContainsKey(code);
}
```

### 3.3 Domain Layer -- Return Data Models

The 103 return types fall into three structural categories discovered from the schema analysis.

**Category 1: Fixed-Row Returns** (single record per submission, ~15 tables):

These tables like `mfcr_300`, `mfcr_1000`, `mfcr_100`, `fc_acr`, `fc_car_2` have a known fixed set of columns, each mapping to a CBN line code (e.g., 10110, 30120). One row per submission.

```csharp
namespace FC.Engine.Domain.Returns.FixedRow;

public class Mfcr300Data : IReturnData
{
    public ReturnCode ReturnCode => ReturnCode.Parse("MFCR 300");
    
    // Cash
    [LineCode("10110")] public decimal? CashNotes { get; set; }
    [LineCode("10120")] public decimal? CashCoins { get; set; }
    [LineCode("10140")] public decimal? TotalCash { get; set; }
    
    // Due From Banks Nigeria
    [LineCode("10170")] public decimal? DueFromBanksNigeria { get; set; }
    [LineCode("10180")] public decimal? UnclearedEffects { get; set; }
    [LineCode("10185")] public decimal? DueFromOtherFi { get; set; }
    [LineCode("10190")] public decimal? TotalDueFromBanksNigeria { get; set; }
    // ... all other fields matching schema columns
    
    [LineCode("10670")] public decimal? TotalAssets { get; set; }
    [LineCode("10750")] public decimal? TotalLiabilities { get; set; }
    [LineCode("10830")] public decimal? TotalEquity { get; set; }
    [LineCode("10840")] public decimal? TotalLiabilitiesAndEquity { get; set; }
}
```

**Category 2: Multi-Row Returns** (~70 tables with `serial_no`):

Tables like `mfcr_302`, `mfcr_304`, `mfcr_318`, `mfcr_349`, `qfcr_364`, `sfcr_1930` store variable-length lists of line items.

```csharp
namespace FC.Engine.Domain.Returns.MultiRow;

public class Mfcr302DataSet : IReturnData
{
    public ReturnCode ReturnCode => ReturnCode.Parse("MFCR 302");
    public List<Mfcr302Row> Rows { get; set; } = new();
    public decimal? TotalAmount => Rows.Sum(r => r.AmountNgn);
}

public class Mfcr302Row
{
    public string? BankCode { get; set; }
    public string? InstitutionName { get; set; }
    public string? InstitutionType { get; set; }
    public string? AccountNumber { get; set; }
    public decimal? AmountNgn { get; set; }
    public string? CurrencyType { get; set; }
    public string? ClearedUncleared { get; set; }
}
```

**Category 3: Item-Coded Returns** (~18 tables with `item_code`):

Tables like `mfcr_356`, `mfcr_358`, `mfcr_362`, `mfcr_1002`, `mfcr_1020`, `mfcr_1540`, `mfcr_1550`, `fc_car_1` use a fixed set of item_code rows that each have specific meaning.

```csharp
namespace FC.Engine.Domain.Returns.ItemCoded;

public class Mfcr1002DataSet : IReturnData
{
    public ReturnCode ReturnCode => ReturnCode.Parse("MFCR 1002");
    public List<Mfcr1002Item> Items { get; set; } = new();
    
    public decimal? GetTotalInterestIncome() 
        => Items.Sum(i => i.InterestIncome);
    
    public Mfcr1002Item? GetByCode(int code) 
        => Items.FirstOrDefault(i => i.ItemCode == code);
}

public class Mfcr1002Item
{
    public int ItemCode { get; set; }
    public string? ItemDescription { get; set; }
    public decimal? BookValue { get; set; }
    public decimal? CouponRate { get; set; }
    public decimal? InterestIncome { get; set; }
    public decimal? FairValueGainLoss { get; set; }
    public decimal? GainLossOnDisposal { get; set; }
    public decimal? TotalAmount { get; set; }
}
```

### 3.4 Application Layer -- Orchestrators

**`IngestionOrchestrator.cs`**:

```csharp
namespace FC.Engine.Application.Services;

public class IngestionOrchestrator
{
    private readonly IXmlParser _xmlParser;
    private readonly IXsdSchemaProvider _schemaProvider;
    private readonly IValidationEngine _validationEngine;
    private readonly ISubmissionRepository _submissionRepo;
    private readonly IReturnRepository _returnRepo;
    
    /// <summary>
    /// Full pipeline: Parse XML -> Validate XSD -> Map to Domain -> 
    /// Run all validations -> Persist or Reject
    /// </summary>
    public async Task<SubmissionResult> ProcessSubmission(
        Stream xmlStream, 
        string returnCode, 
        int institutionId, 
        int returnPeriodId,
        CancellationToken ct)
    {
        // 1. Create submission record in DRAFT status
        var submission = Submission.Create(institutionId, returnPeriodId, returnCode);
        await _submissionRepo.Add(submission, ct);
        
        // 2. XSD validation (structural)
        var xsd = _schemaProvider.GetSchema(ReturnCode.Parse(returnCode));
        var xsdErrors = _xmlParser.ValidateAgainstXsd(xmlStream, xsd);
        if (xsdErrors.Any())
            return SubmissionResult.SchemaFailed(submission.Id, xsdErrors);
        
        // 3. Parse XML into domain model
        xmlStream.Position = 0;
        var returnData = _xmlParser.Parse(xmlStream, ReturnCode.Parse(returnCode));
        
        // 4. Run validation engine
        submission.MarkValidating();
        var report = await _validationEngine.Validate(returnData, submission, ct);
        
        // 5. Decision
        if (report.IsValid)
        {
            await _returnRepo.Save(returnData, submission.Id, ct);
            submission.MarkAccepted();
        }
        else if (report.HasWarnings && !report.HasErrors)
        {
            await _returnRepo.Save(returnData, submission.Id, ct);
            submission.MarkAcceptedWithWarnings();
        }
        else
        {
            submission.MarkRejected();
        }
        
        submission.AttachValidationReport(report);
        await _submissionRepo.Update(submission, ct);
        
        return new SubmissionResult(submission.Id, submission.Status, report);
    }
}
```

**`ValidationOrchestrator.cs`** -- Runs all rule categories in sequence:

```csharp
namespace FC.Engine.Application.Services;

public class ValidationOrchestrator : IValidationEngine
{
    private readonly IRuleRegistry _ruleRegistry;
    private readonly IReturnRepository _returnRepo;
    
    public async Task<ValidationReport> Validate(
        IReturnData data, 
        Submission submission, 
        CancellationToken ct)
    {
        var report = new ValidationReport(submission.Id);
        var returnCode = data.ReturnCode;
        
        // Phase 1: Type and range validation (applied generically via reflection/attributes)
        var typeRules = _ruleRegistry.GetTypeRules(returnCode);
        foreach (var rule in typeRules)
            report.AddErrors(rule.Validate(data));
        
        // Phase 2: Intra-sheet formula validation
        var formulaRules = _ruleRegistry.GetIntraSheetRules(returnCode);
        foreach (var rule in formulaRules)
            report.AddErrors(rule.Validate(data));
        
        // Phase 3: Cross-sheet validation 
        // Load other returns for same institution + period from DB
        var crossRules = _ruleRegistry.GetCrossSheetRules(returnCode);
        if (crossRules.Any())
        {
            var context = await BuildCrossSheetContext(submission, crossRules, ct);
            context.Register(returnCode, data);
            foreach (var rule in crossRules)
                report.AddErrors(rule.Validate(context));
        }
        
        // Phase 4: Business rules
        var bizRules = _ruleRegistry.GetBusinessRules(returnCode);
        var bizContext = new BusinessRuleContext(submission.ReturnPeriod);
        foreach (var rule in bizRules)
            report.AddErrors(rule.Validate(data, bizContext));
        
        report.FinalizeAt(DateTime.UtcNow);
        return report;
    }
    
    private async Task<CrossSheetValidationContext> BuildCrossSheetContext(
        Submission submission, 
        IEnumerable<ICrossSheetRule> rules, 
        CancellationToken ct)
    {
        var context = new CrossSheetValidationContext();
        var requiredCodes = rules
            .SelectMany(r => r.RequiredReturnCodes)
            .Distinct();
        
        foreach (var code in requiredCodes)
        {
            var existingData = await _returnRepo.GetBySubmissionPeriod(
                submission.InstitutionId, 
                submission.ReturnPeriodId, 
                code, ct);
            if (existingData != null)
                context.Register(code, existingData);
        }
        
        return context;
    }
}
```

### 3.5 Infrastructure Layer -- Validation Rules (Examples)

**Intra-sheet rule for MFCR 300 (sums)**:

```csharp
namespace FC.Engine.Infrastructure.Validation.Rules.IntraSheet;

public class Mfcr300SumRules : IIntraSheetRule
{
    public string RuleId => "MFCR300_SUMS";
    public ReturnCode ApplicableReturnCode => ReturnCode.Parse("MFCR 300");
    public ValidationCategory Category => ValidationCategory.IntraSheet;
    public ValidationSeverity DefaultSeverity => ValidationSeverity.Error;
    
    public IEnumerable<ValidationError> Validate(IReturnData data)
    {
        var d = (Mfcr300Data)data;
        var errors = new List<ValidationError>();
        
        // total_cash = cash_notes + cash_coins
        CheckSum(errors, "total_cash", d.TotalCash, 
            d.CashNotes, d.CashCoins);
        
        // total_due_from_banks_nigeria = due_from_banks_nigeria + uncleared_effects + due_from_other_fi
        CheckSum(errors, "total_due_from_banks_nigeria", d.TotalDueFromBanksNigeria,
            d.DueFromBanksNigeria, d.UnclearedEffects, d.DueFromOtherFi);
        
        // total_money_at_call = money_at_call_secured + money_at_call_unsecured
        CheckSum(errors, "total_money_at_call", d.TotalMoneyAtCall,
            d.MoneyAtCallSecured, d.MoneyAtCallUnsecured);
        
        // total_bank_placements = secured + unsecured + discount_houses
        CheckSum(errors, "total_bank_placements", d.TotalBankPlacements,
            d.PlacementsSecuredBanks, d.PlacementsUnsecuredBanks, d.PlacementsDiscountHouses);
        
        // total_securities = sum of all security types
        CheckSum(errors, "total_securities", d.TotalSecurities,
            d.TreasuryBills, d.FgnBonds, d.StateGovtBonds, d.LocalGovtBonds,
            d.CorporateBonds, d.OtherBonds, d.TreasuryCertificates,
            d.CbnRegisteredCertificates, d.CertificatesOfDeposit, d.CommercialPapers);
        
        // total_gross_loans = sum of all loan categories
        CheckSum(errors, "total_gross_loans", d.TotalGrossLoans,
            d.LoansToFiNigeria, d.LoansToSubsidiaryNigeria, d.LoansToSubsidiaryOutside,
            d.LoansToAssociateNigeria, d.LoansToAssociateOutside,
            d.LoansToOtherEntitiesOutside, d.LoansToGovernment, d.LoansToOtherCustomers);
        
        // total_net_loans = total_gross_loans - impairment_on_loans
        CheckDifference(errors, "total_net_loans", d.TotalNetLoans,
            d.TotalGrossLoans, d.ImpairmentOnLoans);
        
        // total_assets balance check
        CheckSum(errors, "total_assets", d.TotalAssets,
            d.TotalCash, d.TotalDueFromBanksNigeria, d.TotalDueFromBanksOutside,
            d.TotalMoneyAtCall, d.TotalBankPlacements, d.DerivativeFinancialAssets,
            d.TotalSecurities, d.TotalNetLoans, d.OtherInvestmentsQuoted,
            d.OtherInvestmentsUnquoted, d.InvestmentsInSubsidiaries,
            d.InvestmentsInAssociates, d.OtherAssets, d.IntangibleAssets,
            d.NonCurrentAssetsHeldForSale, d.PropertyPlantEquipment);
        
        // total_liabilities_and_equity = total_liabilities + total_equity
        CheckSum(errors, "total_liabilities_and_equity", d.TotalLiabilitiesAndEquity,
            d.TotalLiabilities, d.TotalEquity);
        
        // Balance sheet must balance: total_assets == total_liabilities_and_equity
        if (d.TotalAssets.HasValue && d.TotalLiabilitiesAndEquity.HasValue 
            && d.TotalAssets != d.TotalLiabilitiesAndEquity)
        {
            errors.Add(new ValidationError
            {
                RuleId = "MFCR300_BALANCE_SHEET",
                Field = "total_assets vs total_liabilities_and_equity",
                Message = "Balance sheet does not balance",
                ExpectedValue = d.TotalLiabilitiesAndEquity?.ToString("N2"),
                ActualValue = d.TotalAssets?.ToString("N2"),
                Severity = ValidationSeverity.Error,
                Category = ValidationCategory.IntraSheet
            });
        }
        
        return errors;
    }
    
    private void CheckSum(List<ValidationError> errors, string field, 
        decimal? actual, params decimal?[] addends)
    {
        var expected = addends.Where(a => a.HasValue).Sum(a => a!.Value);
        if (actual.HasValue && actual.Value != expected)
        {
            errors.Add(new ValidationError
            {
                RuleId = $"MFCR300_SUM_{field.ToUpper()}",
                Field = field,
                Message = $"Sum validation failed for {field}",
                ExpectedValue = expected.ToString("N2"),
                ActualValue = actual.Value.ToString("N2"),
                Severity = DefaultSeverity,
                Category = Category
            });
        }
    }
}
```

**Cross-sheet rule: MFCR 302 total must equal MFCR 300 line 10170**:

```csharp
namespace FC.Engine.Infrastructure.Validation.Rules.CrossSheet;

public class Mfcr302ToMfcr300Rule : ICrossSheetRule
{
    public string RuleId => "XSHEET_MFCR302_TO_300_10170";
    public ValidationCategory Category => ValidationCategory.CrossSheet;
    public ValidationSeverity DefaultSeverity => ValidationSeverity.Error;
    
    public IReadOnlyList<ReturnCode> RequiredReturnCodes => new[]
    {
        ReturnCode.Parse("MFCR 300"),
        ReturnCode.Parse("MFCR 302")
    };
    
    public IEnumerable<ValidationError> Validate(CrossSheetValidationContext context)
    {
        var errors = new List<ValidationError>();
        
        var mfcr300 = context.GetReturn<Mfcr300Data>(ReturnCode.Parse("MFCR 300"));
        var mfcr302 = context.GetReturn<Mfcr302DataSet>(ReturnCode.Parse("MFCR 302"));
        
        if (mfcr300 == null || mfcr302 == null)
        {
            // Cannot validate; one of the required returns has not been submitted yet
            errors.Add(new ValidationError
            {
                RuleId = RuleId,
                Field = "cross_sheet_dependency",
                Message = "Cross-sheet validation deferred: MFCR 300 or MFCR 302 not yet submitted",
                Severity = ValidationSeverity.Warning,
                Category = Category
            });
            return errors;
        }
        
        var mfcr302Total = mfcr302.TotalAmount;
        var mfcr300Line = mfcr300.DueFromBanksNigeria; // line 10170
        
        if (mfcr302Total.HasValue && mfcr300Line.HasValue 
            && mfcr302Total.Value != mfcr300Line.Value)
        {
            errors.Add(new ValidationError
            {
                RuleId = RuleId,
                Field = "MFCR302.total vs MFCR300.due_from_banks_nigeria",
                Message = "MFCR 302 total must equal MFCR 300 line 10170 (due_from_banks_nigeria)",
                ExpectedValue = mfcr300Line.Value.ToString("N2"),
                ActualValue = mfcr302Total.Value.ToString("N2"),
                ReferencedReturnCode = "MFCR 300",
                Severity = DefaultSeverity,
                Category = Category
            });
        }
        
        return errors;
    }
}
```

### 3.6 Infrastructure Layer -- Rule Registry

```csharp
namespace FC.Engine.Infrastructure.Validation;

public class RuleRegistry : IRuleRegistry
{
    private readonly Dictionary<ReturnCode, List<IIntraSheetRule>> _intraRules = new();
    private readonly Dictionary<ReturnCode, List<ICrossSheetRule>> _crossRules = new();
    private readonly Dictionary<ReturnCode, List<IBusinessRule>> _bizRules = new();
    
    public RuleRegistry(
        IEnumerable<IIntraSheetRule> intraSheetRules,
        IEnumerable<ICrossSheetRule> crossSheetRules,
        IEnumerable<IBusinessRule> businessRules)
    {
        // Index rules by applicable return code(s)
        foreach (var rule in intraSheetRules)
            _intraRules.GetOrAdd(rule.ApplicableReturnCode).Add(rule);
        
        foreach (var rule in crossSheetRules)
            foreach (var code in rule.RequiredReturnCodes)
                _crossRules.GetOrAdd(code).Add(rule);
        
        // Business rules registered via attributes or explicit mapping
    }
    
    // DI registration uses assembly scanning:
    // services.Scan(s => s.FromAssemblyOf<RuleRegistry>()
    //     .AddClasses(c => c.AssignableTo<IIntraSheetRule>())
    //     .AsImplementedInterfaces()
    //     .WithTransientLifetime());
}
```

### 3.7 Infrastructure Layer -- XML/XSD

**XSD Design** -- Each return code gets one XSD. The namespace convention is `urn:cbn:dfis:fc:{return_code_lower}`. Example for MFCR 300:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
           targetNamespace="urn:cbn:dfis:fc:mfcr300"
           xmlns:tns="urn:cbn:dfis:fc:mfcr300"
           elementFormDefault="qualified">

  <xs:element name="MFCR300">
    <xs:complexType>
      <xs:sequence>
        <xs:element name="Header" type="tns:HeaderType"/>
        <xs:element name="FinancialPosition" type="tns:FinancialPositionType"/>
      </xs:sequence>
    </xs:complexType>
  </xs:element>

  <xs:complexType name="HeaderType">
    <xs:sequence>
      <xs:element name="InstitutionCode" type="xs:string"/>
      <xs:element name="ReportingDate" type="xs:date"/>
      <xs:element name="ReturnCode" type="xs:string" fixed="MFCR300"/>
    </xs:sequence>
  </xs:complexType>

  <xs:complexType name="FinancialPositionType">
    <xs:sequence>
      <!-- Cash -->
      <xs:element name="CashNotes" type="tns:MoneyType" minOccurs="0"/>
      <xs:element name="CashCoins" type="tns:MoneyType" minOccurs="0"/>
      <xs:element name="TotalCash" type="tns:MoneyType" minOccurs="0"/>
      <!-- ... all 80+ fields ... -->
    </xs:sequence>
  </xs:complexType>

  <xs:simpleType name="MoneyType">
    <xs:restriction base="xs:decimal">
      <xs:fractionDigits value="2"/>
      <xs:totalDigits value="20"/>
    </xs:restriction>
  </xs:simpleType>

</xs:schema>
```

For multi-row returns like MFCR 302:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
           targetNamespace="urn:cbn:dfis:fc:mfcr302"
           xmlns:tns="urn:cbn:dfis:fc:mfcr302"
           elementFormDefault="qualified">

  <xs:element name="MFCR302">
    <xs:complexType>
      <xs:sequence>
        <xs:element name="Header" type="tns:HeaderType"/>
        <xs:element name="Balances">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="BankBalance" type="tns:BankBalanceType" 
                          minOccurs="0" maxOccurs="unbounded"/>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
      </xs:sequence>
    </xs:complexType>
  </xs:element>

  <xs:complexType name="BankBalanceType">
    <xs:sequence>
      <xs:element name="BankCode" type="xs:string"/>
      <xs:element name="InstitutionName" type="xs:string"/>
      <xs:element name="InstitutionType" type="xs:string" minOccurs="0"/>
      <xs:element name="AccountNumber" type="xs:string" minOccurs="0"/>
      <xs:element name="AmountNGN" type="tns:MoneyType"/>
      <xs:element name="CurrencyType" type="tns:CurrencyEnum"/>
      <xs:element name="ClearedUncleared" type="tns:ClearedEnum" minOccurs="0"/>
    </xs:sequence>
  </xs:complexType>

  <xs:simpleType name="CurrencyEnum">
    <xs:restriction base="xs:string">
      <xs:enumeration value="NGN"/>
      <xs:enumeration value="USD"/>
      <xs:enumeration value="GBP"/>
      <xs:enumeration value="EUR"/>
    </xs:restriction>
  </xs:simpleType>
</xs:schema>
```

**`XmlReturnParser.cs`**:

```csharp
namespace FC.Engine.Infrastructure.Xml;

public class XmlReturnParser : IXmlParser
{
    private readonly IXsdSchemaProvider _schemaProvider;
    private readonly XmlParserFactory _parserFactory;
    
    public IReadOnlyList<ValidationError> ValidateAgainstXsd(Stream xml, XmlSchemaSet schema)
    {
        var errors = new List<ValidationError>();
        var settings = new XmlReaderSettings();
        settings.Schemas = schema;
        settings.ValidationType = ValidationType.Schema;
        settings.ValidationEventHandler += (s, e) =>
        {
            errors.Add(new ValidationError
            {
                RuleId = "XSD_VALIDATION",
                Field = e.Exception?.SourceUri ?? "unknown",
                Message = e.Message,
                Severity = e.Severity == XmlSeverityType.Error 
                    ? ValidationSeverity.Error : ValidationSeverity.Warning,
                Category = ValidationCategory.Schema
            });
        };
        
        using var reader = XmlReader.Create(xml, settings);
        while (reader.Read()) { } // Force full read for validation
        return errors;
    }
    
    public IReturnData Parse(Stream xml, ReturnCode returnCode)
    {
        var parser = _parserFactory.GetParser(returnCode);
        return parser.Parse(xml);
    }
}
```

### 3.8 Infrastructure Layer -- EF Core DbContext

```csharp
namespace FC.Engine.Infrastructure.Persistence;

public class FcEngineDbContext : DbContext
{
    // Reference tables
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<ReturnPeriod> ReturnPeriods => Set<ReturnPeriod>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<BankCode> BankCodes => Set<BankCode>();
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<SubSector> SubSectors => Set<SubSector>();
    public DbSet<State> States => Set<State>();
    public DbSet<LocalGovernment> LocalGovernments => Set<LocalGovernment>();
    
    // Return data tables (103)
    public DbSet<Mfcr300Entity> Mfcr300 => Set<Mfcr300Entity>();
    public DbSet<Mfcr1000Entity> Mfcr1000 => Set<Mfcr1000Entity>();
    public DbSet<Mfcr100Entity> Mfcr100 => Set<Mfcr100Entity>();
    public DbSet<Mfcr302Entity> Mfcr302 => Set<Mfcr302Entity>();
    // ... all 103 tables
    
    // Validation
    public DbSet<ValidationReportEntity> ValidationReports => Set<ValidationReportEntity>();
    public DbSet<ValidationErrorEntity> ValidationErrors => Set<ValidationErrorEntity>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FcEngineDbContext).Assembly);
    }
}
```

Note: The EF entity classes (e.g., `Mfcr300Entity`) are distinct from the domain return data classes (`Mfcr300Data`). A mapping layer converts between them. This keeps the domain clean of ORM concerns.

### 3.9 API Layer

```csharp
namespace FC.Engine.Api.Endpoints;

public static class SubmissionEndpoints
{
    public static void MapSubmissionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/submissions").WithTags("Submissions");
        
        // POST /api/submissions/{returnCode}
        // Body: XML content
        // Headers: X-Institution-Code, X-Reporting-Date
        group.MapPost("/{returnCode}", async (
            string returnCode,
            [FromHeader(Name = "X-Institution-Code")] string institutionCode,
            [FromHeader(Name = "X-Reporting-Date")] DateOnly reportingDate,
            HttpRequest request,
            IngestionOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var result = await orchestrator.ProcessSubmission(
                request.Body, returnCode, institutionCode, reportingDate, ct);
            
            return result.Status switch
            {
                SubmissionStatus.Accepted => Results.Ok(result.ToDto()),
                SubmissionStatus.AcceptedWithWarnings => Results.Ok(result.ToDto()),
                SubmissionStatus.Rejected => Results.UnprocessableEntity(result.ToDto()),
                _ => Results.StatusCode(500)
            };
        })
        .Accepts<IFormFile>("application/xml")
        .Produces<SubmissionResultDto>(200)
        .Produces<SubmissionResultDto>(422);
        
        // POST /api/submissions/batch
        // Accepts multipart form with multiple XML files
        group.MapPost("/batch", async (
            [FromHeader(Name = "X-Institution-Code")] string institutionCode,
            [FromHeader(Name = "X-Reporting-Date")] DateOnly reportingDate,
            IFormFileCollection files,
            IngestionOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            // Process all files, collect results, then run cross-sheet validation
        });
        
        // GET /api/submissions/{id}
        group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetSubmissionQuery(id), ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });
        
        // GET /api/submissions/{id}/validation-report
        group.MapGet("/{id:int}/validation-report", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var report = await mediator.Send(new GetValidationReportQuery(id), ct);
            return report is not null ? Results.Ok(report) : Results.NotFound();
        });
    }
}
```

### 3.10 Docker Compose

```yaml
# docker-compose.yml
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=${SA_PASSWORD}
      - MSSQL_PID=Developer
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql
    healthcheck:
      test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$$SA_PASSWORD" -C -Q "SELECT 1"
      interval: 10s
      retries: 10

  migrator:
    build:
      context: .
      dockerfile: docker/Dockerfile.migrator
    depends_on:
      sqlserver:
        condition: service_healthy
    environment:
      - ConnectionStrings__FcEngine=Server=sqlserver;Database=FcEngine;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True

  api:
    build:
      context: .
      dockerfile: docker/Dockerfile.api
    depends_on:
      migrator:
        condition: service_completed_successfully
    ports:
      - "5000:8080"
    environment:
      - ConnectionStrings__FcEngine=Server=sqlserver;Database=FcEngine;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True
      - ASPNETCORE_ENVIRONMENT=Production

volumes:
  sqlserver-data:
```

---

## 4. Complete Cross-Sheet Validation Map

Based on the requirements and schema analysis, here is the full cross-sheet validation matrix:

| Rule ID | Source Return | Source Field/Total | Target Return | Target Field (Line Code) | Description |
|---------|-------------|-------------------|---------------|-------------------------|-------------|
| XS-001 | MFCR 302 | SUM(amount_ngn) where cleared | MFCR 300 | due_from_banks_nigeria (10170) | Bank balances -> fin position |
| XS-002 | MFCR 302 | SUM(amount_ngn) where uncleared | MFCR 300 | uncleared_effects (10180) | Uncleared effects |
| XS-003 | MFCR 304 | SUM(amount_ngn) secured | MFCR 300 | money_at_call_secured (10250) | Secured call money |
| XS-004 | MFCR 306 | SUM(amount_ngn) | MFCR 300 | money_at_call_unsecured (10260) | Unsecured call money |
| XS-005 | MFCR 308 | SUM(amount_ngn) | MFCR 300 | placements_secured_banks (10280) | Secured placements |
| XS-006 | MFCR 310 | SUM(amount_ngn) | MFCR 300 | placements_unsecured_banks (10290) | Unsecured placements |
| XS-007 | MFCR 312 | SUM(amount_ngn) | MFCR 300 | placements_discount_houses (10295) | DH placements |
| XS-008 | MFCR 316 | SUM(carrying_value) | MFCR 300 | derivative_financial_assets (10370) | Derivative assets |
| XS-009 | MFCR 318 | SUM(net_carrying_value) | MFCR 300 | treasury_bills (10380) | T-bills |
| XS-010 | MFCR 320 | SUM(market_value) | MFCR 300 | fgn_bonds (10390) | FGN bonds |
| XS-011 | MFCR 322 | SUM(market_value) | MFCR 300 | state_govt_bonds (10400) | State bonds |
| XS-012 | MFCR 324 | SUM(market_value) | MFCR 300 | local_govt_bonds (10410) | Local bonds |
| XS-013 | MFCR 326 | SUM(market_value) | MFCR 300 | corporate_bonds (10420) | Corporate bonds |
| XS-014 | MFCR 328 | SUM(market_value) | MFCR 300 | other_bonds (10430) | Other bonds |
| XS-015 | MFCR 330 | SUM(net_carrying_value) | MFCR 300 | treasury_certificates (10440) | Treasury certs |
| XS-016 | MFCR 334 | SUM(amount_ngn) | MFCR 300 | certificates_of_deposit (10460) | CDs |
| XS-017 | MFCR 336 | SUM(market_value) | MFCR 300 | commercial_papers (10470) | Commercial papers |
| XS-018 | MFCR 338 | SUM(carrying_amount) | MFCR 300 | loans_to_fi_nigeria (10490) | Loans to FIs |
| XS-019 | MFCR 340 | SUM(total) | MFCR 300 | loans_to_subsidiary_nigeria (10500) | Subsidiary loans NG |
| XS-020 | MFCR 342 | SUM(total) | MFCR 300 | loans_to_subsidiary_outside (10510) | Subsidiary loans outside |
| XS-021 | MFCR 344 | SUM(total) | MFCR 300 | loans_to_associate_nigeria (10520) | Associate loans NG |
| XS-022 | MFCR 346 | SUM(total) | MFCR 300 | loans_to_associate_outside (10530) | Associate loans outside |
| XS-023 | MFCR 348 | SUM(carrying_amount) | MFCR 300 | loans_to_other_entities_outside (10540) | Other entity loans |
| XS-024 | MFCR 351 | SUM(carrying_amount) | MFCR 300 | loans_to_government (10545) | Government loans |
| XS-025 | MFCR 349 | SUM(carrying_amount) | MFCR 300 | loans_to_other_customers (10550) | Customer loans |
| XS-026 | MFCR 350 | SUM(impairment_for_period) | MFCR 300 | impairment_on_loans (10570) | Impairment |
| XS-027 | MFCR 352 | SUM(carrying_value_end) quoted | MFCR 300 | other_investments_quoted (10590) | Quoted investments |
| XS-028 | MFCR 352 | SUM(carrying_value_end) unquoted | MFCR 300 | other_investments_unquoted (10600) | Unquoted investments |
| XS-029 | MFCR 354 | SUM() subsidiary | MFCR 300 | investments_in_subsidiaries (10610) | Sub investments |
| XS-030 | MFCR 354 | SUM() associate | MFCR 300 | investments_in_associates (10620) | Assoc investments |
| XS-031 | MFCR 356+357 | SUM(total) | MFCR 300 | other_assets (10630) | Other assets |
| XS-032 | MFCR 358 | SUM(total) | MFCR 300 | intangible_assets (10640) | Intangibles |
| XS-033 | MFCR 360 | SUM(total) | MFCR 300 | non_current_assets_held_for_sale (10650) | NCAFS |
| XS-034 | MFCR 362 | SUM(net_carrying_amount) | MFCR 300 | property_plant_equipment (10660) | PPE |
| XS-035 | QFCR 377 | SUM(amount_ngn) | MFCR 300 | borrowings_from_banks (10680) | Borrowings from banks |
| XS-036 | QFCR 379 | SUM(amount_ngn) | MFCR 300 | borrowings_from_other_fc (10690) | Borrowings from FCs |
| XS-037 | QFCR 381 | SUM(amount_ngn) | MFCR 300 | borrowings_from_other_fi (10700) | Borrowings from FIs |
| XS-038 | MFCR 1530 | SUM(amount_borrowed) | MFCR 300 | borrowings_from_individuals (10710) | Individual borrowings |
| XS-039 | MFCR 385 | SUM(carrying_value) | MFCR 300 | derivative_financial_liabilities (10730) | Derivative liabilities |
| XS-040 | MFCR 387 | SUM(total) | MFCR 300 | other_liabilities (10740) | Other liabilities |
| XS-041 | MFCR 1002 | SUM(total_amount) | MFCR 1000 | interest_income_govt_securities (30140) | Govt sec income |
| XS-042 | MFCR 387 | item 34715 (unaudited profit) | MFCR 1000 | profit_after_tax (30600) | Profit cross-check |
| XS-043 | FC CAR 1 | derived from MFCR 300 | MFCR 300 | asset_value column | Risk-weighted assets |
| XS-044 | FC ACR | capital_funds | MFCR 300 | total_equity (10830) | ACR capital check |
| XS-045 | FC ACR | net_credit | MFCR 300 | total_gross_loans (10560) | ACR credit check |

---

## 5. Phased Implementation Order

### Phase 1: Foundation (Weeks 1-2)

**Goal**: Solution scaffolding, database, reference data, and one complete end-to-end return (MFCR 300).

**Files to create**:

```
Phase 1A - Solution Structure:
  FCEngine.sln
  src/FC.Engine.Domain/FC.Engine.Domain.csproj
  src/FC.Engine.Application/FC.Engine.Application.csproj
  src/FC.Engine.Infrastructure/FC.Engine.Infrastructure.csproj
  src/FC.Engine.Api/FC.Engine.Api.csproj
  src/FC.Engine.Migrator/FC.Engine.Migrator.csproj
  docker/docker-compose.yml
  docker/Dockerfile.api
  docker/Dockerfile.migrator
  docker/.env.example

Phase 1B - Domain Core:
  src/FC.Engine.Domain/Entities/Submission.cs
  src/FC.Engine.Domain/Entities/Institution.cs
  src/FC.Engine.Domain/Entities/ReturnPeriod.cs
  src/FC.Engine.Domain/Entities/ReturnHeader.cs
  src/FC.Engine.Domain/Entities/ValidationReport.cs
  src/FC.Engine.Domain/Entities/ValidationError.cs
  src/FC.Engine.Domain/ValueObjects/ReturnCode.cs
  src/FC.Engine.Domain/ValueObjects/ReportingPeriod.cs
  src/FC.Engine.Domain/ValueObjects/MoneyAmount.cs
  src/FC.Engine.Domain/ValueObjects/LineCode.cs
  src/FC.Engine.Domain/Enums/SubmissionStatus.cs
  src/FC.Engine.Domain/Enums/ReturnFrequency.cs
  src/FC.Engine.Domain/Enums/ValidationSeverity.cs
  src/FC.Engine.Domain/Enums/ValidationCategory.cs
  src/FC.Engine.Domain/Returns/IReturnData.cs
  src/FC.Engine.Domain/Validation/IValidationRule.cs
  src/FC.Engine.Domain/Validation/IIntraSheetRule.cs
  src/FC.Engine.Domain/Validation/ICrossSheetRule.cs
  src/FC.Engine.Domain/Validation/IBusinessRule.cs
  src/FC.Engine.Domain/Validation/ValidationContext.cs
  src/FC.Engine.Domain/Validation/FormulaExpression.cs
  src/FC.Engine.Domain/Abstractions/IReturnRepository.cs
  src/FC.Engine.Domain/Abstractions/ISubmissionRepository.cs
  src/FC.Engine.Domain/Abstractions/IXmlParser.cs
  src/FC.Engine.Domain/Abstractions/IValidationEngine.cs
  src/FC.Engine.Domain/Abstractions/IRuleRegistry.cs

Phase 1C - First Return (MFCR 300):
  src/FC.Engine.Domain/Returns/FixedRow/Mfcr300Data.cs
  src/FC.Engine.Infrastructure/Persistence/FcEngineDbContext.cs
  src/FC.Engine.Infrastructure/Persistence/Configurations/SubmissionConfiguration.cs
  src/FC.Engine.Infrastructure/Persistence/Configurations/InstitutionConfiguration.cs
  src/FC.Engine.Infrastructure/Persistence/Configurations/ReturnPeriodConfiguration.cs
  src/FC.Engine.Infrastructure/Persistence/Configurations/Mfcr300Configuration.cs
  src/FC.Engine.Infrastructure/Persistence/Configurations/ValidationReportConfiguration.cs
  src/FC.Engine.Infrastructure/Persistence/Repositories/SubmissionRepository.cs
  src/FC.Engine.Infrastructure/Persistence/Repositories/ReturnRepository.cs
  src/FC.Engine.Infrastructure/Xml/Schemas/MFCR300.xsd
  src/FC.Engine.Infrastructure/Xml/XsdSchemaProvider.cs
  src/FC.Engine.Infrastructure/Xml/XmlReturnParser.cs
  src/FC.Engine.Infrastructure/Xml/Parsers/IReturnXmlParser.cs
  src/FC.Engine.Infrastructure/Xml/Parsers/Mfcr300XmlParser.cs
  src/FC.Engine.Infrastructure/Xml/XmlParserFactory.cs
  src/FC.Engine.Infrastructure/Validation/RuleEngine.cs
  src/FC.Engine.Infrastructure/Validation/RuleRegistry.cs
  src/FC.Engine.Infrastructure/Validation/Rules/IntraSheet/Mfcr300SumRules.cs
  src/FC.Engine.Infrastructure/DependencyInjection.cs

Phase 1D - Application + API:
  src/FC.Engine.Application/DependencyInjection.cs
  src/FC.Engine.Application/Services/IngestionOrchestrator.cs
  src/FC.Engine.Application/Services/ValidationOrchestrator.cs
  src/FC.Engine.Application/DTOs/SubmissionDto.cs
  src/FC.Engine.Application/DTOs/ValidationReportDto.cs
  src/FC.Engine.Api/Program.cs
  src/FC.Engine.Api/appsettings.json
  src/FC.Engine.Api/Endpoints/SubmissionEndpoints.cs
  src/FC.Engine.Api/Endpoints/SchemaEndpoints.cs
  src/FC.Engine.Api/Middleware/ExceptionHandlingMiddleware.cs
  src/FC.Engine.Migrator/Program.cs
  scripts/seed-reference-data.sql

Phase 1E - Tests:
  tests/FC.Engine.Domain.Tests/FC.Engine.Domain.Tests.csproj
  tests/FC.Engine.Domain.Tests/ValueObjects/ReturnCodeTests.cs
  tests/FC.Engine.Infrastructure.Tests/FC.Engine.Infrastructure.Tests.csproj
  tests/FC.Engine.Infrastructure.Tests/Xml/Mfcr300XmlParserTests.cs
  tests/FC.Engine.Infrastructure.Tests/Validation/Mfcr300SumRulesTests.cs
  tests/FC.Engine.Integration.Tests/FC.Engine.Integration.Tests.csproj
  tests/FC.Engine.Integration.Tests/SubmissionPipelineTests.cs
```

**Milestone**: Can submit MFCR 300 XML, validate its internal sums, persist to SQL Server, get validation report back. Docker Compose starts up cleanly.

---

### Phase 2: Core Monthly Returns (Weeks 3-4)

**Goal**: All MFCR 300-series returns (the financial position schedules) plus MFCR 1000 (income statement). Cross-sheet validation between 300-series schedules and MFCR 300.

**Returns to implement** (31 returns):

```
MFCR 300 (done), MFCR 1000, MFCR 100
MFCR 302, 304, 306, 306-1, 308, 310, 312, 314, 314-1
MFCR 316, 318, 320, 322, 324, 326, 328, 330, 332, 334, 334-1, 336, 336-1
MFCR 338, 340, 342, 344, 346, 346-1
```

**Files per return** (template -- multiply by ~30):

```
For each return (example: MFCR 302):
  src/FC.Engine.Domain/Returns/MultiRow/Mfcr302Row.cs
  src/FC.Engine.Infrastructure/Persistence/Configurations/Mfcr302Configuration.cs
  src/FC.Engine.Infrastructure/Xml/Schemas/MFCR302.xsd
  src/FC.Engine.Infrastructure/Xml/Parsers/Mfcr302XmlParser.cs
  src/FC.Engine.Infrastructure/Validation/Rules/CrossSheet/Mfcr302ToMfcr300Rule.cs
  tests/FC.Engine.Infrastructure.Tests/Xml/Mfcr302XmlParserTests.cs
```

**Additional cross-sheet rules**:

```
  src/FC.Engine.Infrastructure/Validation/Rules/CrossSheet/Mfcr302ToMfcr300Rule.cs  (XS-001, XS-002)
  src/FC.Engine.Infrastructure/Validation/Rules/CrossSheet/Mfcr304ToMfcr300Rule.cs  (XS-003)
  src/FC.Engine.Infrastructure/Validation/Rules/CrossSheet/Mfcr306ToMfcr300Rule.cs  (XS-004)
  ... (XS-005 through XS-017 for all security/placement schedules)
  src/FC.Engine.Infrastructure/Validation/Rules/CrossSheet/Mfcr338ToMfcr300Rule.cs  (XS-018 through XS-026 for loan schedules)
  tests/FC.Engine.Integration.Tests/CrossSheetValidationTests.cs
```

**Application layer additions**:

```
  src/FC.Engine.Application/Services/CrossSheetValidationService.cs
  src/FC.Engine.Application/Commands/SubmitBatch/SubmitBatchCommand.cs
  src/FC.Engine.Application/Commands/SubmitBatch/SubmitBatchHandler.cs
```

**Milestone**: All 300-series returns parse, validate intra-sheet, and cross-validate against MFCR 300 line items. Batch submission API works.

---

### Phase 3: Remaining Monthly Returns (Weeks 5-6)

**Goal**: Income breakdown returns (1000-series), impairments, other assets/liabilities, contingencies.

**Returns to implement** (~35 returns):

```
MFCR 348, 349, 350, 351, 351(2)
MFCR 352, 354, 356, 357, 358, 360, 362
QFCR 364, 366, 368, 370, 372, 374
MFCR 376, QFCR 376-1
QFCR 377, 379, 381, 381-1
MFCR 334-1, 385, 387, 388, 395, 397, 397-1
MFCR 1002, 1004, 1006, 1008, 1010, 1012, 1014, 1016, 1018, 1018-1, 1020
```

**Cross-sheet rules added**:

```
  XS-027 through XS-042 (investment, liability, income cross-checks)
  MFCR 1000 to MFCR 1002 (govt securities income)
  MFCR 387 to MFCR 1000 (unaudited profit cross-check)
```

**Milestone**: All monthly-frequency returns operational. Complete income statement and balance sheet validation chain.

---

### Phase 4: Quarterly, Semi-Annual, and Specialized Returns (Weeks 7-8)

**Goal**: Lending/borrowing returns (1500-series), semi-annual corporate returns (1900-series), capital adequacy, and derived returns.

**Returns to implement** (~25 returns):

```
MFCR 1510, 1520, 1530, 1540, 1550
MFCR 1570, 1590, 1600, 1610, 1620, 1630
SFCR 1900, 1910, 1920, 1930, 1940, 1950, 1960
FC CAR 1, FC CAR 2, FC ACR, FC FHR, FC CVR, FC RATING
CONSOL, NPL, REPORTS (KRI)
sheet2_return_codes, cleaned_summary, summary, sheet3_top10_rankings
```

**Cross-sheet rules added**:

```
  XS-043 (FC CAR reads from MFCR 300)
  XS-044, XS-045 (FC ACR reads from MFCR 300 + MFCR 1000)
```

**Business rules added**:

```
  src/FC.Engine.Infrastructure/Validation/Rules/Business/DateInPastRule.cs
  src/FC.Engine.Infrastructure/Validation/Rules/Business/DecimalPrecisionRule.cs
  src/FC.Engine.Infrastructure/Validation/Rules/Business/NonZeroRequiredFieldRule.cs
  src/FC.Engine.Infrastructure/Validation/Rules/Business/DropdownValueRule.cs
  src/FC.Engine.Infrastructure/Validation/Rules/Business/ConditionalRequiredRule.cs
  src/FC.Engine.Infrastructure/Validation/Rules/Business/CapitalAdequacyMinimumRule.cs
  src/FC.Engine.Infrastructure/Validation/Rules/Business/LendingLimitRule.cs
```

**Milestone**: All 103 returns implemented. Full CBN compliance validation.

---

### Phase 5: Hardening, Reporting, and Polish (Weeks 9-10)

**Goal**: Production readiness.

```
Files:
  src/FC.Engine.Api/Middleware/RequestLoggingMiddleware.cs
  src/FC.Engine.Api/Endpoints/ValidationEndpoints.cs
  src/FC.Engine.Api/Endpoints/ReturnDataEndpoints.cs
  src/FC.Engine.Api/Endpoints/ReferenceDataEndpoints.cs
  src/FC.Engine.Application/Queries/GetSubmission/GetSubmissionQuery.cs
  src/FC.Engine.Application/Queries/GetSubmission/GetSubmissionHandler.cs
  src/FC.Engine.Application/Queries/GetValidationReport/GetValidationReportQuery.cs
  src/FC.Engine.Application/Queries/GetValidationReport/GetValidationReportHandler.cs
  src/FC.Engine.Application/Queries/GetReturnData/GetReturnDataQuery.cs
  src/FC.Engine.Application/Queries/GetReturnData/GetReturnDataHandler.cs
  docker/docker-compose.override.yml (dev overrides)
  docs/return-codes.md
  docs/validation-rules.md
  docs/api-reference.md
  docs/cross-sheet-map.md

Tasks:
  - Add health check endpoints (/health, /ready)
  - Add structured logging (Serilog)
  - Add OpenAPI/Swagger documentation
  - Add rate limiting
  - Add response compression
  - Performance test with full submission batch
  - Comprehensive integration test suite
  - Load test with concurrent submissions
```

---

## 6. Key Design Decisions and Trade-offs

### 6.1 Why Separate EF Entities from Domain Models

The domain `Mfcr300Data` and EF `Mfcr300Entity` are separate classes with a mapping layer between them. Rationale:
- Domain models can have computed properties, validation logic, and business methods without ORM leak
- EF entities can be flat, table-mapped POCOs optimized for persistence
- Prevents EF change tracking from interfering with validation logic
- The mapping cost is negligible compared to XML parsing + DB I/O

### 6.2 Why Not Code-Generate All 103 Return Types

While tempting, code generation from the Excel/SQL schema introduces fragility:
- Each return has unique validation semantics that require human understanding
- The three structural categories (FixedRow, MultiRow, ItemCoded) have different code patterns
- However, within each category, many tables share identical structures (e.g., QFCR 364-376 all have the same column layout). For these, we use a generic base class with column-name configuration, reducing the per-return boilerplate.

### 6.3 Why JSON Rule Definitions + Code Rules (Hybrid)

Simple sum/difference formulas can be expressed in JSON configuration:

```json
{
  "returnCode": "MFCR300",
  "rules": [
    {
      "ruleId": "MFCR300_SUM_CASH",
      "type": "sum",
      "target": "total_cash",
      "addends": ["cash_notes", "cash_coins"]
    }
  ]
}
```

Complex cross-sheet rules and conditional business rules require C# code. The hybrid approach means:
- Simple rules can be updated without recompilation (loaded from JSON at startup)
- Complex rules get the full power of C# with compile-time safety
- The `FormulaEvaluator` class interprets JSON rules; coded rules implement interfaces directly

### 6.4 Cross-Sheet Validation Timing

Cross-sheet validation cannot run until both sides of a cross-reference have been submitted. Two strategies:

**Strategy A (Recommended for Phase 1)**: Validate at batch submission time. The batch endpoint accepts all related returns together and runs cross-sheet after all are parsed.

**Strategy B (Phase 2 enhancement)**: Deferred cross-sheet validation. When submitting a single return, cross-sheet rules produce warnings if the counterpart is not yet submitted. When the counterpart arrives, re-run cross-sheet rules for both.

### 6.5 Schema Conversion: PostgreSQL to SQL Server

The existing `schema.sql` uses PostgreSQL syntax (`SERIAL`, `NUMERIC`, `TIMESTAMP`, `BOOLEAN`). For SQL Server:
- `SERIAL PRIMARY KEY` becomes `INT IDENTITY(1,1) PRIMARY KEY`
- `NUMERIC(20,2)` stays as `DECIMAL(20,2)`
- `TIMESTAMP DEFAULT CURRENT_TIMESTAMP` becomes `DATETIME2 DEFAULT GETUTCDATE()`
- `BOOLEAN` becomes `BIT`
- `TEXT` stays as `NVARCHAR(MAX)`

EF Core migrations will handle this conversion naturally when we define entities with Fluent API configurations targeting SQL Server.

---

## 7. Dependency Graph

```
FC.Engine.Api
  |-- FC.Engine.Application
  |     |-- FC.Engine.Domain
  |-- FC.Engine.Infrastructure
        |-- FC.Engine.Domain

FC.Engine.Migrator
  |-- FC.Engine.Infrastructure
        |-- FC.Engine.Domain
```

NuGet packages per project:

```
FC.Engine.Domain:
  (no external dependencies -- pure C#)

FC.Engine.Application:
  MediatR (optional, for CQRS)
  FluentValidation (for command validation)

FC.Engine.Infrastructure:
  Microsoft.EntityFrameworkCore.SqlServer
  Microsoft.EntityFrameworkCore.Design (for migrations)
  System.Xml.XmlSerializer (built-in)

FC.Engine.Api:
  Microsoft.AspNetCore.OpenApi
  Swashbuckle.AspNetCore
  Serilog.AspNetCore
  Serilog.Sinks.Console

FC.Engine.Migrator:
  Microsoft.EntityFrameworkCore.Design
  Microsoft.EntityFrameworkCore.SqlServer

Tests:
  xunit
  FluentAssertions
  NSubstitute
  Microsoft.EntityFrameworkCore.InMemory (or Testcontainers.MsSql)
  Testcontainers.MsSql (for integration tests)
```

---

## 8. Return Type Classification Summary

From the full schema analysis, here is how each of the 103 data tables (excluding 8 reference tables) maps to a structural category:

**Fixed-Row (single record per submission, ~8 tables)**:
`mfcr_300`, `mfcr_1000`, `mfcr_100`, `fc_car_2`, `fc_acr`, `fc_rating`, `sfcr_1910`

**Multi-Row with serial_no (~60 tables)**:
`mfcr_302`, `mfcr_304`, `mfcr_306`, `mfcr_306_1`, `mfcr_308`, `mfcr_310`, `mfcr_312`, `mfcr_314`, `mfcr_314_1`, `mfcr_316`, `mfcr_318`, `mfcr_320`, `mfcr_322`, `mfcr_324`, `mfcr_326`, `mfcr_328`, `mfcr_330`, `mfcr_332`, `mfcr_334`, `mfcr_334_1`, `mfcr_336`, `mfcr_336_1`, `mfcr_338`, `mfcr_340`, `mfcr_342`, `mfcr_344`, `mfcr_346`, `mfcr_346_1`, `mfcr_348`, `mfcr_349`, `mfcr_350`, `mfcr_351`, `mfcr_351_2`, `mfcr_352`, `mfcr_354`, `mfcr_360`, `mfcr_376`, `mfcr_385`, `mfcr_388`, `mfcr_395`, `mfcr_397`, `mfcr_397_1`, `qfcr_364`, `qfcr_366`, `qfcr_368`, `qfcr_370`, `qfcr_372`, `qfcr_374`, `qfcr_376_1`, `qfcr_377`, `qfcr_379`, `qfcr_381`, `qfcr_381_1`, `mfcr_1004`, `mfcr_1008`, `mfcr_1012`, `mfcr_1014`, `mfcr_1016`, `mfcr_1018`, `mfcr_1018_1`, `mfcr_1510`, `mfcr_1520`, `mfcr_1530`, `mfcr_1570`, `mfcr_1590`, `mfcr_1600`, `mfcr_1610`, `mfcr_1620`, `sfcr_1900`, `sfcr_1920`, `sfcr_1930`, `sfcr_1940`, `sfcr_1950`, `sfcr_1960`, `npl`

**Item-Coded with item_code (~15 tables)**:
`mfcr_356`, `mfcr_357`, `mfcr_358`, `mfcr_362`, `mfcr_387`, `mfcr_1002`, `mfcr_1006`, `mfcr_1010`, `mfcr_1020`, `mfcr_1540`, `mfcr_1550`, `fc_car_1`, `fc_fhr`, `fc_cvr`

**Aggregation/Report tables** (not per-submission):
`consol`, `reports_kri`, `sheet3_top10_rankings`, `sheet2_return_codes`, `cleaned_summary`, `summary`

---

## 9. Potential Challenges and Mitigations

| Challenge | Mitigation |
|-----------|-----------|
| 103 XSD schemas to create | Create 3 XSD template generators (one per structural category). Many multi-row tables share identical column layouts (e.g., QFCR 364-376). |
| 103 EF configurations | Use a convention-based `IEntityTypeConfiguration<T>` with reflection for common patterns. Only custom configs for tables with unique constraints. |
| Formula catalog is in Excel binary format | Extract the 508 formulas into a JSON catalog (manual or scripted via Python/openpyxl). This JSON becomes the authoritative formula registry. |
| Cross-sheet validation ordering | Batch submission API ensures all returns arrive together. Deferred validation with re-run on second arrival as Phase 2 enhancement. |
| Performance with 103 tables | Lazy-load cross-sheet dependencies only when needed. Index on `(institution_id, return_period_id, return_code)` ensures fast lookups. |
| PostgreSQL schema to SQL Server | EF Core migrations generate SQL Server DDL natively. The existing `schema.sql` serves as documentation, not as migration source. |

---

### Critical Files for Implementation

- `/Users/mac/codes/fcs/schema.sql` - The complete 2487-line SQL schema with all 111 tables, column definitions, line codes, and indexes. This is the single source of truth for every return template's data structure and serves as the blueprint for all EF Core entity configurations, domain models, and XSD schemas.
- `/Users/mac/Downloads/formula_catalog_dfis_fc_return_templates.xlsx` - Contains all 508 formula cells across 17 sheets. Must be parsed (via Python openpyxl or manual extraction) into a JSON rule catalog that drives both the intra-sheet validation rules and cross-sheet validation map.
- `/Users/mac/codes/fcs/FC Engine/` (new) `src/FC.Engine.Infrastructure/Validation/RuleRegistry.cs` - The central registry that indexes all validation rules by return code. This is the architectural keystone connecting 103 return types to their applicable intra-sheet, cross-sheet, and business rules.
- `/Users/mac/codes/fcs/FC Engine/` (new) `src/FC.Engine.Application/Services/IngestionOrchestrator.cs` - The pipeline controller that orchestrates the full submission lifecycle: XML parsing, XSD validation, domain mapping, multi-phase validation, and persistence. Every API call flows through this class.
- `/Users/mac/codes/fcs/FC Engine/` (new) `src/FC.Engine.Infrastructure/Persistence/FcEngineDbContext.cs` - The EF Core DbContext with DbSets for all 111+ tables. This file, along with its 103+ entity configurations, represents the largest volume of infrastructure code and must accurately mirror schema.sql for SQL Server.