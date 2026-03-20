using FC.Engine.Domain.Abstractions;
using FC.Engine.Infrastructure.BackgroundJobs;
using FC.Engine.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FC.Engine.Infrastructure;

public static class ConductRiskServiceExtensions
{
    public static IServiceCollection AddConductRiskSurveillance(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IRegulatorTenantResolver, RegulatorTenantResolver>();

        services.AddScoped<IBDCFXSurveillance, BDCFXSurveillance>();
        services.AddScoped<ICMOSurveillance, CMOSurveillance>();
        services.AddScoped<IInsuranceConductMonitor, InsuranceConductMonitor>();
        services.AddScoped<IAMLConductMonitor, AMLConductMonitor>();
        services.AddScoped<IConductRiskScorer, ConductRiskScorer>();
        services.AddScoped<ISurveillanceOrchestrator, SurveillanceOrchestrator>();
        services.AddScoped<IAlertManagementService, AlertManagementService>();
        services.AddScoped<IWhistleblowerService, WhistleblowerService>();

        services.AddHostedService<SurveillanceCycleBackgroundService>();

        return services;
    }
}
