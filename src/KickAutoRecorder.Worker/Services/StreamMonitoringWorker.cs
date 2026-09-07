using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Worker.Services;

public class StreamMonitoringWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<StreamMonitoringWorker> _logger;

    public StreamMonitoringWorker(
        IServiceProvider serviceProvider,
        ISettingsService settingsService,
        ILogger<StreamMonitoringWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KickVideo StreamMonitoringWorker background service starting...");

        // 1. Run Startup Crash Recovery Scan
        using (var scope = _serviceProvider.CreateScope())
        {
            var orchestrator = scope.ServiceProvider.GetRequiredService<IRecordingOrchestrator>();
            await orchestrator.RecoverUnfinishedRecordingsAsync(stoppingToken);
        }

        var initialInterval = _settingsService.GetSettings().PollingIntervalSeconds;
        _logger.LogInformation("Background polling loop initialized with {Interval}s interval.", initialInterval);

        // 2. Main Background Polling Loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var streamerService = scope.ServiceProvider.GetRequiredService<IStreamerService>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IRecordingOrchestrator>();

                var streamers = await streamerService.GetAllStreamersAsync(stoppingToken);
                var activeStreamers = streamers.Where(s => s.IsTrackingEnabled).ToList();

                _logger.LogDebug("Monitoring tick: Checking status for {Count} active streamers.", activeStreamers.Count);

                foreach (var streamer in activeStreamers)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    try
                    {
                        await orchestrator.ProcessStreamerCheckAsync(streamer, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing streamer check for {Username}", streamer.Username);
                    }

                    // 1-second delay between channels to respect Kick platform API rate limits
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("StreamMonitoringWorker polling loop canceled via stoppingToken.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception encountered in StreamMonitoringWorker loop tick.");
            }

            try
            {
                var interval = _settingsService.GetSettings().PollingIntervalSeconds;
                await Task.Delay(TimeSpan.FromSeconds(interval > 0 ? interval : 30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("KickVideo StreamMonitoringWorker loop terminated.");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Host shutdown initiated. Stopping all active FFmpeg recordings gracefully...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IRecordingOrchestrator>();
            await orchestrator.StopAllActiveRecordingsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during StopAsync graceful recording shutdown.");
        }

        await base.StopAsync(cancellationToken);
    }
}
