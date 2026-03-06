using FC.Engine.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.BackgroundJobs;

public class ExportProcessingJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExportProcessingJob> _logger;

    public ExportProcessingJob(
        IServiceProvider serviceProvider,
        ILogger<ExportProcessingJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var exportRequestRepository = scope.ServiceProvider.GetRequiredService<IExportRequestRepository>();
                var exportEngine = scope.ServiceProvider.GetRequiredService<IExportEngine>();

                var queued = await exportRequestRepository.GetQueuedBatch(5, stoppingToken);
                foreach (var request in queued)
                {
                    var result = await exportEngine.GenerateExport(request.Id, stoppingToken);
                    if (!result.Success)
                    {
                        _logger.LogWarning(
                            "Export request {RequestId} failed: {Error}",
                            request.Id,
                            result.ErrorMessage);
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Export processing cycle failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }
}
