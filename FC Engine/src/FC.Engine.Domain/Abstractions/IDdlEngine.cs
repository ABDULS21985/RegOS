using FC.Engine.Domain.Metadata;

namespace FC.Engine.Domain.Abstractions;

public interface IDdlEngine
{
    DdlScript GenerateCreateTable(ReturnTemplate template, TemplateVersion version);
    DdlScript GenerateAlterTable(ReturnTemplate template, TemplateVersion oldVersion, TemplateVersion newVersion);
}

public record DdlScript(string ForwardSql, string RollbackSql);
