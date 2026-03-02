using FC.Engine.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FC.Engine.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IngestionOrchestrator>();
        return services;
    }
}
