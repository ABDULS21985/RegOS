using FC.Engine.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.BackgroundJobs;

public class ExportCleanupJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExportCleanupJob> _logger;

    public ExportCleanupJob(
        IServiceProvider serviceProvider,
        ILogger<ExportCleanupJob> logger)
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
                var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

                var expired = await exportRequestRepository.GetExpired(DateTime.UtcNow, 100, stoppingToken);
                foreach (var request in expired)
                {
                    await RemoveExpiredExport(request, exportRequestRepository, fileStorageService, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Export cleanup cycle failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private async Task RemoveExpiredExport(
        Domain.Entities.ExportRequest request,
        IExportRequestRepository exportRequestRepository,
        IFileStorageService fileStorageService,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.FilePath))
        {
            try
            {
                await fileStorageService.DeleteAsync(request.FilePath, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed deleting expired export file {Path}", request.FilePath);
            }
        }

        request.FilePath = null;
        request.FileSize = null;
        request.Sha256Hash = null;
        request.ErrorMessage = "Export expired and has been removed from storage.";
        await exportRequestRepository.Update(request, ct);
    }
}
