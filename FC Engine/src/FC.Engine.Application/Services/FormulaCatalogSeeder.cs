using System.Text.Json;
using System.Text.Json.Serialization;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Domain.Validation;

namespace FC.Engine.Application.Services;

/// <summary>
/// Seeds intra-sheet formulas and cross-sheet rules from formula_catalog.json
/// which was extracted from the official CBN DFIS FC Return Templates Excel file.
/// Formulas use item codes (line codes) as defined in the CBN templates.
/// </summary>
public class FormulaCatalogSeeder
{
    private readonly ITemplateRepository _templateRepo;
    private readonly IFormulaRepository _formulaRepo;

    public FormulaCatalogSeeder(ITemplateRepository templateRepo, IFormulaRepository formulaRepo)
    {
        _templateRepo = templateRepo;
        _formulaRepo = formulaRepo;
    }

    public async Task<FormulaCatalogSeedResult> SeedFromCatalog(
        string catalogFilePath, string performedBy, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(catalogFilePath, ct);
        var catalog = JsonSerializer.Deserialize<FormulaCatalog>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }) ?? throw new InvalidOperationException("Failed to parse formula catalog JSON");

        var result = new FormulaCatalogSeedResult();

        // Seed intra-sheet formulas
        await SeedIntraSheetFormulas(catalog.IntraSheetFormulas, performedBy, result, ct);

        // Seed cross-sheet rules
        await SeedCrossSheetRules(catalog.CrossSheetRules, performedBy, result, ct);

        return result;
    }

    private async Task SeedIntraSheetFormulas(
        List<CatalogIntraSheetFormula> formulas,
        string performedBy,
        FormulaCatalogSeedResult result,
        CancellationToken ct)
    {
        // Group formulas by return code
        var grouped = formulas.GroupBy(f => f.ReturnCode);

        foreach (var group in grouped)
        {
            var returnCode = group.Key;
            try
            {
                var template = await _templateRepo.GetByReturnCode(returnCode, ct);
                if (template == null)
                {
                    result.Errors.Add($"{returnCode}: Template not found");
                    continue;
                }

                var publishedVersion = template.CurrentPublishedVersion;
                if (publishedVersion == null)
                {
                    result.Errors.Add($"{returnCode}: No published version");
                    continue;
                }

                // Skip if formulas already seeded
                if (publishedVersion.IntraSheetFormulas.Any())
                {
                    result.Skipped.Add(returnCode);
                    continue;
                }

                // Build line_code → field_name mapping
                var lineCodeToField = publishedVersion.Fields
                    .Where(f => !string.IsNullOrEmpty(f.LineCode))
                    .GroupBy(f => f.LineCode!)
                    .ToDictionary(g => g.Key, g => g.First());

                var sortOrder = 0;
                var formulasCreated = 0;

                foreach (var catalogFormula in group)
                {
                    // Map target item code to field
                    if (!lineCodeToField.TryGetValue(catalogFormula.TargetItemCode, out var targetField))
                    {
                        result.Warnings.Add(
                            $"{returnCode}: Target item code {catalogFormula.TargetItemCode} " +
                            $"({catalogFormula.TargetDescription}) not found in template fields");
                        continue;
                    }

                    // Map operand item codes to field names
                    var operandFieldNames = new List<string>();
                    var operandLineCodes = new List<string>();
                    var allOperandsFound = true;

                    foreach (var opCode in catalogFormula.OperandItemCodes)
                    {
                        if (lineCodeToField.TryGetValue(opCode, out var opField))
                        {
                            operandFieldNames.Add(opField.FieldName);
                            operandLineCodes.Add(opCode);
                        }
                        else
                        {
                            result.Warnings.Add(
                                $"{returnCode}: Operand item code {opCode} not found " +
                                $"(formula for {catalogFormula.TargetItemCode})");
                            allOperandsFound = false;
                        }
                    }

                    if (!allOperandsFound || operandFieldNames.Count < 2)
                    {
                        // Still create the formula but with item codes as reference
                        // This allows manual correction later via Admin Portal
                    }

                    var formulaType = catalogFormula.FormulaType switch
                    {
                        "Sum" => FormulaType.Sum,
                        "Difference" => FormulaType.Difference,
                        "Custom" => FormulaType.Custom,
                        _ => FormulaType.Custom
                    };

                    // Build the custom expression for Custom type formulas
                    // These have mixed + and - operators
                    string? customExpression = null;
                    if (formulaType == FormulaType.Custom)
                    {
                        customExpression = BuildCustomExpression(
                            catalogFormula.FormulaExpression, lineCodeToField);
                    }

                    var ruleCode = $"{returnCode.Replace(" ", "-")}-" +
                        $"{(formulaType == FormulaType.Sum ? "SUM" : formulaType == FormulaType.Difference ? "DIFF" : "EXPR")}-" +
                        $"{++sortOrder:D3}";

                    var formula = new IntraSheetFormula
                    {
                        TemplateVersionId = publishedVersion.Id,
                        RuleCode = ruleCode,
                        RuleName = $"{catalogFormula.TargetDescription}",
                        FormulaType = formulaType,
                        TargetFieldName = targetField.FieldName,
                        TargetLineCode = catalogFormula.TargetItemCode,
                        OperandFields = JsonSerializer.Serialize(
                            operandFieldNames.Any() ? operandFieldNames : catalogFormula.OperandItemCodes),
                        OperandLineCodes = JsonSerializer.Serialize(operandLineCodes),
                        CustomExpression = customExpression,
                        ToleranceAmount = 0.01m,
                        Severity = ValidationSeverity.Error,
                        IsActive = true,
                        SortOrder = sortOrder,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = performedBy
                    };

                    publishedVersion.AddFormula(formula);
                    formulasCreated++;
                }

                if (formulasCreated > 0)
                {
                    await _templateRepo.Update(template, ct);
                    result.TemplatesSeeded.Add(returnCode);
                    result.TotalFormulasCreated += formulasCreated;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{returnCode}: {ex.Message}");
            }
        }
    }

    private async Task SeedCrossSheetRules(
        List<CatalogCrossSheetRef> crossRefs,
        string performedBy,
        FormulaCatalogSeedResult result,
        CancellationToken ct)
    {
        var existingCatalogRules = (await _formulaRepo.GetAllCrossSheetRules(ct) ?? Array.Empty<CrossSheetRule>())
            .Where(rule => rule.RuleCode.StartsWith("XS-FC-", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(rule => rule.RuleCode, rule => rule, StringComparer.OrdinalIgnoreCase);

        var desiredRules = new List<CrossSheetRule>();
        var desiredRuleCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var grouped = crossRefs.GroupBy(r => r.TargetReturnCode);
        var ruleIndex = 0;

        foreach (var group in grouped)
        {
            var targetReturnCode = group.Key;

            foreach (var crossRef in group)
            {
                try
                {
                    // Parse the reference expression to extract source template and item codes
                    var parsed = ParseCrossSheetExpression(crossRef.ReferenceExpression);
                    if (parsed == null)
                    {
                        result.Warnings.Add(
                            $"Cross-sheet: Could not parse expression '{crossRef.ReferenceExpression}' " +
                            $"for {targetReturnCode}");
                        continue;
                    }

                    var targetTemplate = await _templateRepo.GetByReturnCode(targetReturnCode, ct);
                    var targetVersion = targetTemplate?.CurrentPublishedVersion;
                    if (targetVersion == null)
                    {
                        result.Warnings.Add($"Cross-sheet {targetReturnCode}: Target template not found or unpublished");
                        continue;
                    }

                    var sourceTemplate = await _templateRepo.GetByReturnCode(parsed.SourceReturnCode, ct);
                    var sourceVersion = sourceTemplate?.CurrentPublishedVersion;
                    if (sourceVersion == null)
                    {
                        result.Warnings.Add($"Cross-sheet {targetReturnCode}: Source template {parsed.SourceReturnCode} not found or unpublished");
                        continue;
                    }

                    var targetField = ResolveTargetField(targetVersion, crossRef.Description);
                    if (targetField == null)
                    {
                        result.Warnings.Add(
                            $"Cross-sheet {targetReturnCode}: Could not map target description '{crossRef.Description}' to a template field");
                        continue;
                    }

                    var sourceField = ResolveSourceField(sourceVersion, parsed.SourceItemCode);
                    if (sourceField == null)
                    {
                        result.Warnings.Add(
                            $"Cross-sheet {targetReturnCode}: Could not map source item code {parsed.SourceItemCode} on {parsed.SourceReturnCode} to a template field");
                        continue;
                    }

                    var ruleCode = $"XS-FC-{++ruleIndex:D3}";
                    var rule = new CrossSheetRule
                    {
                        RuleCode = ruleCode,
                        RuleName = $"{targetReturnCode} references {parsed.SourceReturnCode}:{parsed.SourceItemCode}",
                        Description = string.IsNullOrWhiteSpace(crossRef.Description)
                            ? crossRef.ReferenceExpression
                            : crossRef.Description,
                        Severity = ValidationSeverity.Error,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = performedBy,
                        Expression = new CrossSheetRuleExpression
                        {
                            Expression = parsed.Expression,
                            ToleranceAmount = 0.01m,
                            ErrorMessage = $"Value mismatch: {targetReturnCode} field should equal {parsed.SourceReturnCode}:{parsed.SourceItemCode}"
                        }
                    };

                    var operands = new List<CrossSheetRuleOperand>
                    {
                        new()
                        {
                            OperandAlias = "A",
                            TemplateReturnCode = targetReturnCode,
                            FieldName = targetField.FieldName,
                            LineCode = targetField.LineCode,
                            SortOrder = 1
                        },
                        new()
                        {
                            OperandAlias = "B",
                            TemplateReturnCode = parsed.SourceReturnCode,
                            FieldName = sourceField.FieldName,
                            LineCode = parsed.SourceItemCode,
                            SortOrder = 2
                        }
                    };

                    rule.SetOperands(operands);
                    desiredRules.Add(rule);
                    desiredRuleCodes.Add(ruleCode);
                }
                catch (Exception ex)
                {
                    result.Warnings.Add(
                        $"Cross-sheet {targetReturnCode}: {ex.Message}");
                }
            }
        }

        foreach (var desiredRule in desiredRules)
        {
            if (!existingCatalogRules.TryGetValue(desiredRule.RuleCode, out var existingRule))
            {
                await _formulaRepo.AddCrossSheetRule(desiredRule, ct);
                result.TotalCrossSheetRulesCreated++;
                continue;
            }

            if (!NeedsCrossSheetRuleUpdate(existingRule, desiredRule))
            {
                continue;
            }

            ApplyCrossSheetRuleDefinition(existingRule, desiredRule);
            await _formulaRepo.UpdateCrossSheetRule(existingRule, ct);
            result.TotalCrossSheetRulesCreated++;
        }

        foreach (var obsoleteRule in existingCatalogRules.Values.Where(rule =>
                     rule.IsActive &&
                     !desiredRuleCodes.Contains(rule.RuleCode)))
        {
            await _formulaRepo.DeleteCrossSheetRule(obsoleteRule.Id, ct);
        }
    }

    /// <summary>
    /// Builds a custom expression by mapping item codes to field names.
    /// E.g., "10420+10430+10440-10510" → "treasury_bills_fvtpl + treasury_bills_fvoci + ... - impairment_treasury_bills"
    /// </summary>
    private static string? BuildCustomExpression(
        string formulaExpression,
        Dictionary<string, TemplateField> lineCodeToField)
    {
        // Replace item codes with field names where possible
        var expr = formulaExpression.Trim();
        var result = expr;

        // Find all numeric tokens (item codes) and replace with field names
        var tokens = System.Text.RegularExpressions.Regex.Matches(expr, @"\d{4,5}");
        // Process in reverse order to avoid offset issues
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            var token = tokens[i];
            if (lineCodeToField.TryGetValue(token.Value, out var field))
            {
                result = result.Substring(0, token.Index) +
                         field.FieldName +
                         result.Substring(token.Index + token.Length);
            }
        }

        return result;
    }

    private static ParsedCrossSheetRef? ParseCrossSheetExpression(string expr)
    {
        // Parse patterns like:
        // ='MFCR 300: 10140 (Total cash)
        // MFCR 300: 10180
        // MFCR300:10640
        var cleaned = expr.Replace("='", "").Replace("'", "").Replace("=", "").Trim();

        // Only seed direct one-to-one references. Multi-operand expressions need explicit field mapping first.
        if (cleaned.IndexOfAny(['+', '-', '*', '/']) >= 0)
        {
            return null;
        }

        var matches = System.Text.RegularExpressions.Regex.Matches(
            cleaned,
            @"(MFCR|QFCR|SFCR)\s*(\d+)\s*[:\s]\s*(\d{4,5})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (matches.Count != 1) return null;

        var match = matches[0];

        var prefix = match.Groups[1].Value;
        var number = match.Groups[2].Value;
        var itemCode = match.Groups[3].Value;

        return new ParsedCrossSheetRef
        {
            SourceReturnCode = $"{prefix} {number}",
            SourceItemCode = itemCode,
            Expression = "A = B"
        };
    }

    private static TemplateField? ResolveTargetField(TemplateVersion version, string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var itemCodeMatch = System.Text.RegularExpressions.Regex.Match(description, @"\b(\d{4,5})\b");
        if (itemCodeMatch.Success)
        {
            var fieldByLineCode = version.Fields.FirstOrDefault(field =>
                string.Equals(field.LineCode, itemCodeMatch.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
            if (fieldByLineCode != null)
            {
                return fieldByLineCode;
            }
        }

        var normalizedDescription = NormalizeFieldToken(description);
        return version.Fields.FirstOrDefault(field =>
            NormalizeFieldToken(field.DisplayName) == normalizedDescription ||
            NormalizeFieldToken(field.FieldName) == normalizedDescription ||
            NormalizeFieldToken(field.XmlElementName) == normalizedDescription);
    }

    private static TemplateField? ResolveSourceField(TemplateVersion version, string sourceItemCode)
    {
        return version.Fields.FirstOrDefault(field =>
            string.Equals(field.LineCode, sourceItemCode, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeFieldToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex
            .Replace(value, @"[^A-Za-z0-9]+", string.Empty)
            .ToUpperInvariant();
    }

    private static bool NeedsCrossSheetRuleUpdate(CrossSheetRule existing, CrossSheetRule desired)
    {
        if (!existing.IsActive) return true;
        if (!string.Equals(existing.RuleName, desired.RuleName, StringComparison.Ordinal)) return true;
        if (!string.Equals(existing.Description, desired.Description, StringComparison.Ordinal)) return true;
        if (existing.Severity != desired.Severity) return true;
        if (existing.Expression is null != desired.Expression is null) return true;

        if (existing.Expression != null && desired.Expression != null)
        {
            if (!string.Equals(existing.Expression.Expression, desired.Expression.Expression, StringComparison.Ordinal)) return true;
            if (existing.Expression.ToleranceAmount != desired.Expression.ToleranceAmount) return true;
            if (existing.Expression.TolerancePercent != desired.Expression.TolerancePercent) return true;
            if (!string.Equals(existing.Expression.ErrorMessage, desired.Expression.ErrorMessage, StringComparison.Ordinal)) return true;
        }

        var existingOperands = existing.Operands.OrderBy(operand => operand.SortOrder).ToList();
        var desiredOperands = desired.Operands.OrderBy(operand => operand.SortOrder).ToList();
        if (existingOperands.Count != desiredOperands.Count) return true;

        for (var i = 0; i < existingOperands.Count; i++)
        {
            var current = existingOperands[i];
            var seeded = desiredOperands[i];

            if (!string.Equals(current.OperandAlias, seeded.OperandAlias, StringComparison.Ordinal)) return true;
            if (!string.Equals(current.TemplateReturnCode, seeded.TemplateReturnCode, StringComparison.Ordinal)) return true;
            if (!string.Equals(current.FieldName, seeded.FieldName, StringComparison.Ordinal)) return true;
            if (!string.Equals(current.LineCode, seeded.LineCode, StringComparison.Ordinal)) return true;
            if (!string.Equals(current.AggregateFunction, seeded.AggregateFunction, StringComparison.Ordinal)) return true;
            if (!string.Equals(current.FilterItemCode, seeded.FilterItemCode, StringComparison.Ordinal)) return true;
            if (current.SortOrder != seeded.SortOrder) return true;
        }

        return false;
    }

    private static void ApplyCrossSheetRuleDefinition(CrossSheetRule existing, CrossSheetRule desired)
    {
        existing.RuleName = desired.RuleName;
        existing.Description = desired.Description;
        existing.Severity = desired.Severity;
        existing.IsActive = true;
        existing.Expression = desired.Expression is null
            ? null
            : new CrossSheetRuleExpression
            {
                Expression = desired.Expression.Expression,
                ToleranceAmount = desired.Expression.ToleranceAmount,
                TolerancePercent = desired.Expression.TolerancePercent,
                ErrorMessage = desired.Expression.ErrorMessage
            };
        existing.SetOperands(desired.Operands.Select(operand => new CrossSheetRuleOperand
        {
            OperandAlias = operand.OperandAlias,
            TemplateReturnCode = operand.TemplateReturnCode,
            FieldName = operand.FieldName,
            LineCode = operand.LineCode,
            AggregateFunction = operand.AggregateFunction,
            FilterItemCode = operand.FilterItemCode,
            SortOrder = operand.SortOrder
        }));
    }

    private record ParsedCrossSheetRef
    {
        public string SourceReturnCode { get; init; } = "";
        public string SourceItemCode { get; init; } = "";
        public string Expression { get; init; } = "A = B";
    }
}

// JSON deserialization models for formula_catalog.json
public class FormulaCatalog
{
    [JsonPropertyName("intra_sheet_formulas")]
    public List<CatalogIntraSheetFormula> IntraSheetFormulas { get; set; } = new();

    [JsonPropertyName("row_level_formulas")]
    public List<CatalogRowFormula> RowLevelFormulas { get; set; } = new();

    [JsonPropertyName("cross_sheet_rules")]
    public List<CatalogCrossSheetRef> CrossSheetRules { get; set; } = new();

    [JsonPropertyName("aggregate_formulas")]
    public List<CatalogAggregateFormula> AggregateFormulas { get; set; } = new();
}

public class CatalogIntraSheetFormula
{
    [JsonPropertyName("return_code")]
    public string ReturnCode { get; set; } = "";

    [JsonPropertyName("target_item_code")]
    public string TargetItemCode { get; set; } = "";

    [JsonPropertyName("target_description")]
    public string TargetDescription { get; set; } = "";

    [JsonPropertyName("formula_expression")]
    public string FormulaExpression { get; set; } = "";

    [JsonPropertyName("formula_type")]
    public string FormulaType { get; set; } = "";

    [JsonPropertyName("operand_item_codes")]
    public List<string> OperandItemCodes { get; set; } = new();

    [JsonPropertyName("row")]
    public int Row { get; set; }
}

public class CatalogRowFormula
{
    [JsonPropertyName("return_code")]
    public string ReturnCode { get; set; } = "";

    [JsonPropertyName("column_formula")]
    public string ColumnFormula { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("row")]
    public int Row { get; set; }

    [JsonPropertyName("col")]
    public int Col { get; set; }
}

public class CatalogCrossSheetRef
{
    [JsonPropertyName("target_return_code")]
    public string TargetReturnCode { get; set; } = "";

    [JsonPropertyName("reference_expression")]
    public string ReferenceExpression { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("row")]
    public int Row { get; set; }

    [JsonPropertyName("col")]
    public int Col { get; set; }
}

public class CatalogAggregateFormula
{
    [JsonPropertyName("return_code")]
    public string ReturnCode { get; set; } = "";

    [JsonPropertyName("aggregate_expression")]
    public string AggregateExpression { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("row")]
    public int Row { get; set; }

    [JsonPropertyName("col")]
    public int Col { get; set; }
}

public class FormulaCatalogSeedResult
{
    public List<string> TemplatesSeeded { get; set; } = new();
    public List<string> Skipped { get; set; } = new();
    public int TotalFormulasCreated { get; set; }
    public int TotalCrossSheetRulesCreated { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
