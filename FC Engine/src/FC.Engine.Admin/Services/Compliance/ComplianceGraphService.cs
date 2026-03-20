using FC.Engine.Admin.Services;

namespace FC.Engine.Admin.Services.Compliance;

/// <summary>
/// Wraps PlatformIntelligenceService with regulator-focused methods for compliance navigation,
/// obligation tracking, and impact propagation analysis. All data is sourced from the real
/// knowledge graph built from DB entities (modules, templates, fields, submissions, etc.).
/// </summary>
public sealed class ComplianceGraphService
{
    private readonly PlatformIntelligenceService _intelligence;

    public ComplianceGraphService(PlatformIntelligenceService intelligence)
    {
        _intelligence = intelligence;
    }

    public async Task<ComplianceGraphDashboard> GetDashboardAsync(CancellationToken ct = default)
    {
        var ws = await _intelligence.GetWorkspaceAsync(ct);
        var kg = ws.KnowledgeGraph;

        var obligations = kg.InstitutionObligations;
        var overdue = obligations.Count(o => o.Status == "Overdue");
        var dueSoon = obligations.Count(o => o.Status == "Due Soon");
        var filed = obligations.Count(o => o.Status == "Filed");
        var critical = kg.ImpactPropagation.Count(i => i.Signal == "Critical");

        return new ComplianceGraphDashboard
        {
            RegulatorCount = kg.RegulatorCount,
            ModuleCount = kg.ModuleCount,
            TemplateCount = kg.TemplateCount,
            FieldCount = kg.FieldCount,
            RequirementCount = kg.RequirementCount,
            ObligationCount = kg.ObligationCount,
            TotalInstitutionObligations = obligations.Count,
            FiledCount = filed,
            OverdueCount = overdue,
            DueSoonCount = dueSoon,
            CriticalImpactCount = critical,
            CatalogMaterializedAt = kg.CatalogMaterializedAt,
            DossierMaterializedAt = kg.DossierMaterializedAt
        };
    }

    public async Task<ComplianceNavigatorData> GetNavigatorDataAsync(CancellationToken ct = default)
    {
        var ws = await _intelligence.GetWorkspaceAsync(ct);
        var kg = ws.KnowledgeGraph;

        return new ComplianceNavigatorData
        {
            OntologyCoverage = kg.OntologyCoverage,
            RequirementRegister = kg.RequirementRegister,
            Lineage = kg.Lineage,
            NavigatorDetails = kg.NavigatorDetails
        };
    }

    public async Task<ObligationRegisterData> GetObligationRegisterAsync(CancellationToken ct = default)
    {
        var ws = await _intelligence.GetWorkspaceAsync(ct);
        var kg = ws.KnowledgeGraph;

        return new ObligationRegisterData
        {
            LicenceObligations = kg.Obligations,
            InstitutionObligations = kg.InstitutionObligations,
            InstitutionOptions = kg.InstitutionOptions
        };
    }

    public async Task<ImpactPropagationData> GetImpactDataAsync(CancellationToken ct = default)
    {
        var ws = await _intelligence.GetWorkspaceAsync(ct);
        var kg = ws.KnowledgeGraph;

        return new ImpactPropagationData
        {
            ImpactSurfaces = kg.ImpactSurfaces,
            ImpactPropagation = kg.ImpactPropagation,
            NavigatorDetails = kg.NavigatorDetails
        };
    }
}

public sealed class ComplianceGraphDashboard
{
    public int RegulatorCount { get; set; }
    public int ModuleCount { get; set; }
    public int TemplateCount { get; set; }
    public int FieldCount { get; set; }
    public int RequirementCount { get; set; }
    public int ObligationCount { get; set; }
    public int TotalInstitutionObligations { get; set; }
    public int FiledCount { get; set; }
    public int OverdueCount { get; set; }
    public int DueSoonCount { get; set; }
    public int CriticalImpactCount { get; set; }
    public DateTime? CatalogMaterializedAt { get; set; }
    public DateTime? DossierMaterializedAt { get; set; }
}

public sealed class ComplianceNavigatorData
{
    public List<KnowledgeGraphOntologyCoverageRow> OntologyCoverage { get; set; } = [];
    public List<KnowledgeGraphRequirementRegisterRow> RequirementRegister { get; set; } = [];
    public List<KnowledgeGraphLineageRow> Lineage { get; set; } = [];
    public List<KnowledgeGraphNavigatorDetail> NavigatorDetails { get; set; } = [];
}

public sealed class ObligationRegisterData
{
    public List<KnowledgeGraphObligationRow> LicenceObligations { get; set; } = [];
    public List<KnowledgeGraphInstitutionObligationRow> InstitutionObligations { get; set; } = [];
    public List<KnowledgeGraphInstitutionOption> InstitutionOptions { get; set; } = [];
}

public sealed class ImpactPropagationData
{
    public List<KnowledgeGraphImpactRow> ImpactSurfaces { get; set; } = [];
    public List<KnowledgeGraphImpactPropagationRow> ImpactPropagation { get; set; } = [];
    public List<KnowledgeGraphNavigatorDetail> NavigatorDetails { get; set; } = [];
}
