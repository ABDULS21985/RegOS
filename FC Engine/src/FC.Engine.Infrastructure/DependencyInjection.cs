using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Validation;
using FC.Engine.Infrastructure.Persistence;
using FC.Engine.Infrastructure.Persistence.Repositories;
using FC.Engine.Infrastructure.Validation;
using FC.Engine.Infrastructure.Validation.Rules.IntraSheet;
using FC.Engine.Infrastructure.Xml;
using FC.Engine.Infrastructure.Xml.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FC.Engine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // EF Core
        services.AddDbContext<FcEngineDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Repositories
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<IReturnRepository, ReturnRepository>();

        // XML Parsing
        services.AddSingleton<IXsdSchemaProvider, XsdSchemaProvider>();
        services.AddSingleton<IReturnXmlParser, Mfcr300XmlParser>();
        services.AddSingleton<XmlParserFactory>();
        services.AddSingleton<IXmlParser, XmlReturnParser>();

        // Validation Rules
        services.AddSingleton<IIntraSheetRule, Mfcr300SumRules>();

        // Rule Registry & Engine
        services.AddSingleton<IRuleRegistry, RuleRegistry>();
        services.AddScoped<IValidationEngine, RuleEngine>();

        return services;
    }
}
