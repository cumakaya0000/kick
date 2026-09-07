using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Worker.Services;

public class YouTubeUploadWorker : BackgroundService
{
    private readonly IYouTubeUploadQueue _uploadQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INotificationService _notificationService;
    private readonly ILogger<YouTubeUploadWorker> _logger;

    public YouTubeUploadWorker(
        IYouTubeUploadQueue uploadQueue,
        IServiceScopeFactory scopeFactory,
        INotificationService notificationService,
        ILogger<YouTubeUploadWorker> logger)
    {
        _uploadQueue = uploadQueue;
        _scopeFactory = scopeFactory;
        _notificationService = notificationService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("YouTubeUploadWorker started. Monitoring YouTube upload queue...");

        // Startup Recovery Scan
        try
        {
            using var startupScope = _scopeFactory.CreateScope();
            var repo = startupScope.ServiceProvider.GetRequiredService<IYouTubeUploadRepository>();
            var activeJobs = await repo.GetPendingOrUploadingJobsAsync(stoppingToken);

            foreach (var job in activeJobs)
            {
                if (job.Status == YouTubeUploadStatus.Uploading || job.Status == YouTubeUploadStatus.Processing)
                {
                    if (!string.IsNullOrEmpty(job.YouTubeVideoId))
                    {
                        await repo.UpdateStatusAsync(job.Id, YouTubeUploadStatus.Completed, cancellationToken: stoppingToken);
                    }
                    else
                    {
                        _logger.LogInformation("Recovered unfinished YouTube upload Job ID {JobId} from application restart.", job.Id);
                        await repo.UpdateStatusAsync(job.Id, YouTubeUploadStatus.Pending, cancellationToken: stoppingToken);
                        await _uploadQueue.EnqueueAsync(job.Id, stoppingToken);
                    }
                }
                else if (job.Status == YouTubeUploadStatus.Pending || job.Status == YouTubeUploadStatus.RetryScheduled)
                {
                    await _uploadQueue.EnqueueAsync(job.Id, stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed executing startup recovery scan in YouTubeUploadWorker.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await _uploadQueue.DequeueAsync(stoppingToken);
                _logger.LogInformation("Dequeued YouTubeUploadJob ID {JobId} for uploading.", jobId);

                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IYouTubeUploadRepository>();
                var uploadService = scope.ServiceProvider.GetRequiredService<IYouTubeUploadService>();

                var job = await repo.GetByIdAsync(jobId, stoppingToken);
                if (job == null || job.Status == YouTubeUploadStatus.Cancelled)
                {
                    _logger.LogWarning("YouTubeUploadJob ID {JobId} was not found or was cancelled before start.", jobId);
                    continue;
                }

                if (job.Status == YouTubeUploadStatus.WaitingForRender)
                {
                    _logger.LogInformation("YouTubeUploadJob ID {JobId} is still waiting for render completion. Skipping.", jobId);
                    continue;
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                _uploadQueue.RegisterActiveJob(jobId, cts);

                DateTime lastDbUpdate = DateTime.MinValue;
                double lastReportedPercent = -1;

                try
                {
                    await repo.UpdateStatusAsync(jobId, YouTubeUploadStatus.Uploading, cancellationToken: stoppingToken);

                    _notificationService.ShowNotification(
                        "📤 YouTube Yüklemesi Başladı",
                        $"'{job.Title}' YouTube'a yükleniyor...",
                        "Info");

                    var resultJob = await uploadService.ExecuteUploadJobAsync(
                        job,
                        (percent, bytesSent, totalBytes) =>
                        {
                            // Throttled Persistence: Update DB only on 1% changes or 2s intervals
                            var now = DateTime.UtcNow;
                            if (Math.Abs(percent - lastReportedPercent) >= 1.0 || (now - lastDbUpdate).TotalSeconds >= 2.0)
                            {
                                lastReportedPercent = percent;
                                lastDbUpdate = now;
                                _ = repo.UpdateProgressAsync(jobId, percent, bytesSent, totalBytes, cancellationToken: stoppingToken);
                            }
                        },
                        cts.Token);

                    await repo.UpdateAsync(resultJob, stoppingToken);

                    if (resultJob.Status == YouTubeUploadStatus.Completed)
                    {
                        var msg = $"'{resultJob.Title}' YouTube'a yüklendi! Video URL: {resultJob.YouTubeVideoUrl}";
                        if (!string.IsNullOrEmpty(resultJob.ThumbnailWarning))
                        {
                            msg += $"\n⚠️ {resultJob.ThumbnailWarning}";
                        }
                        if (!string.IsNullOrEmpty(resultJob.PlaylistWarning))
                        {
                            msg += $"\n⚠️ {resultJob.PlaylistWarning}";
                        }

                        _notificationService.ShowNotification(
                            "✅ YouTube Yüklemesi Tamamlandı",
                            msg,
                            "Info");
                    }
                    else if (resultJob.Status == YouTubeUploadStatus.RetryScheduled && resultJob.RetryCount < 3)
                    {
                        resultJob.RetryCount++;
                        var delaySeconds = (int)Math.Pow(2, resultJob.RetryCount) * 5;
                        _logger.LogWarning("YouTubeUploadJob ID {JobId} transient failure (Retry {Count}/3). Scheduling retry in {Delay}s. Error: {Error}",
                            jobId, resultJob.RetryCount, delaySeconds, resultJob.LastError);

                        await repo.UpdateAsync(resultJob, stoppingToken);

                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                            await _uploadQueue.EnqueueAsync(jobId, stoppingToken);
                        }, stoppingToken);
                    }
                    else if (resultJob.Status == YouTubeUploadStatus.Failed)
                    {
                        _notificationService.ShowNotification(
                            "❌ YouTube Yüklemesi Başarısız",
                            $"'{resultJob.Title}' yüklenemedi: {resultJob.LastError}",
                            "Warning");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("YouTubeUploadJob ID {JobId} processing was cancelled.", jobId);
                    await repo.UpdateStatusAsync(jobId, YouTubeUploadStatus.Cancelled, "Yükleme kullanıcı veya sistem tarafından iptal edildi.", stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception processing YouTubeUploadJob ID {JobId}", jobId);
                    await repo.UpdateStatusAsync(jobId, YouTubeUploadStatus.Failed, ex.Message, stoppingToken);
                }
                finally
                {
                    _uploadQueue.UnregisterActiveJob(jobId);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in YouTubeUploadWorker loop.");
                await Task.Delay(3000, stoppingToken);
            }
        }

        _logger.LogInformation("YouTubeUploadWorker stopping.");
    }
}
