namespace FC.Engine.Application.Models;

public class ModuleDefinition
{
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleVersion { get; set; } = "1.0.0";
    public string RegulatorCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DefaultFrequency { get; set; } = "Monthly";
    public int? DeadlineOffsetDays { get; set; }
    public int DisplayOrder { get; set; }
    public List<TemplateDef> Templates { get; set; } = new();
    public List<DataFlowDef> InterModuleDataFlows { get; set; } = new();
}

public class TemplateDef
{
    public string ReturnCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Frequency { get; set; } = "Monthly";
    public string StructuralCategory { get; set; } = "FixedRow";
    public string? TablePrefix { get; set; }
    public List<SectionDef> Sections { get; set; } = new();
    public List<FieldDef> Fields { get; set; } = new();
    public List<ItemCodeDef> ItemCodes { get; set; } = new();
    public List<FormulaDef> Formulas { get; set; } = new();
    public List<CrossSheetRuleDef> CrossSheetRules { get; set; } = new();
}

public class FieldDef
{
    public string FieldCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string? Section { get; set; }
    public bool Required { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public int? DecimalPlaces { get; set; }
    public int DisplayOrder { get; set; }
    public bool CarryForward { get; set; }
    public string? HelpText { get; set; }
    public string? RegulatoryReference { get; set; }
    public string? EnumValues { get; set; }
    public string? ValidationNote { get; set; }
}

public class FormulaDef
{
    public string FormulaType { get; set; } = string.Empty;
    public string? TargetField { get; set; }
    public List<string> SourceFields { get; set; } = new();
    public string? CustomFunction { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public string Severity { get; set; } = "Error";
    public decimal? ToleranceAmount { get; set; }
    public decimal? TolerancePercent { get; set; }
    public string? Description { get; set; }
}

public class CrossSheetRuleDef
{
    public string? Description { get; set; }
    public string SourceTemplate { get; set; } = string.Empty;
    public string SourceField { get; set; } = string.Empty;
    public string TargetTemplate { get; set; } = string.Empty;
    public string TargetField { get; set; } = string.Empty;
    public string Operator { get; set; } = "Equals";
    public string Severity { get; set; } = "Error";
    public decimal? ToleranceAmount { get; set; }
    public decimal? TolerancePercent { get; set; }
}

public class DataFlowDef
{
    public string SourceTemplate { get; set; } = string.Empty;
    public string SourceField { get; set; } = string.Empty;
    public string TargetModule { get; set; } = string.Empty;
    public string TargetTemplate { get; set; } = string.Empty;
    public string TargetField { get; set; } = string.Empty;
    public string TransformationType { get; set; } = "DirectCopy";
    public string? TransformFormula { get; set; }
    public string? Description { get; set; }
}

public class SectionDef
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public class ItemCodeDef
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
