using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Worker.Services;

public class VideoJobQueueWorker : BackgroundService
{
    private readonly VideoJobQueue _jobQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INotificationService _notificationService;
    private readonly ILogger<VideoJobQueueWorker> _logger;

    public VideoJobQueueWorker(
        IVideoJobQueue jobQueue,
        IServiceScopeFactory scopeFactory,
        INotificationService notificationService,
        ILogger<VideoJobQueueWorker> logger)
    {
        _jobQueue = (VideoJobQueue)jobQueue;
        _scopeFactory = scopeFactory;
        _notificationService = notificationService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VideoJobQueueWorker started. Waiting for rendering jobs...");

        // On startup, recover any pending or queued jobs in database
        try
        {
            using var startupScope = _scopeFactory.CreateScope();
            var repo = startupScope.ServiceProvider.GetRequiredService<IVideoJobRepository>();
            var pendingJobs = await repo.GetPendingOrProcessingJobsAsync(stoppingToken);

            foreach (var job in pendingJobs)
            {
                if (job.Status == VideoJobStatus.Preparing || job.Status == VideoJobStatus.Rendering || job.Status == VideoJobStatus.Validating)
                {
                    // If app crashed while rendering, mark as Interrupted
                    await repo.UpdateStatusAsync(job.Id, VideoJobStatus.Interrupted, "Render job interrupted by application restart.", stoppingToken);
                }
                else if (job.Status == VideoJobStatus.Pending || job.Status == VideoJobStatus.Queued)
                {
                    await _jobQueue.EnqueueAsync(job.Id, stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover pending VideoJobs on worker startup.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await _jobQueue.DequeueAsync(stoppingToken);
                _logger.LogInformation("Dequeued VideoJob ID {JobId} for background rendering.", jobId);

                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IVideoJobRepository>();
                var engine = scope.ServiceProvider.GetRequiredService<IVideoRenderEngine>();

                var job = await repo.GetByIdAsync(jobId, stoppingToken);
                if (job == null || job.Status == VideoJobStatus.Cancelled)
                {
                    _logger.LogWarning("VideoJob ID {JobId} was not found or was cancelled before execution.", jobId);
                    continue;
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                _jobQueue.RegisterActiveJob(jobId, cts);

                try
                {
                    await repo.UpdateStatusAsync(jobId, VideoJobStatus.Preparing, cancellationToken: stoppingToken);

                    _notificationService.ShowNotification(
                        "🎬 Video Render Başladı",
                        $"'{job.Title}' başlıklı video işleniyor...",
                        "Info");

                    await engine.ExecuteRenderJobAsync(job, cts.Token);

                    await repo.UpdateAsync(job, stoppingToken);

                    _notificationService.ShowNotification(
                        "✅ Video Render Tamamlandı",
                        $"'{job.Title}' videosu hazır! Çıktı: {job.OutputPath}",
                        "Info");

                    // Trigger linked YouTubeUploadJob if present
                    try
                    {
                        var uploadRepo = scope.ServiceProvider.GetRequiredService<IYouTubeUploadRepository>();
                        var uploadQueue = scope.ServiceProvider.GetRequiredService<IYouTubeUploadQueue>();
                        var uploadJob = await uploadRepo.GetByRenderJobIdAsync(jobId, stoppingToken);
                        if (uploadJob != null && uploadJob.Status == YouTubeUploadStatus.WaitingForRender)
                        {
                            if (job.Status == VideoJobStatus.Completed && File.Exists(job.OutputPath))
                            {
                                uploadJob.VideoFilePath = job.OutputPath;
                                uploadJob.Status = YouTubeUploadStatus.Pending;
                                await uploadRepo.UpdateAsync(uploadJob, stoppingToken);
                                await uploadQueue.EnqueueAsync(uploadJob.Id, stoppingToken);
                                _logger.LogInformation("Render completed for Job ID {JobId}. Enqueued linked YouTubeUploadJob ID {UploadId}", jobId, uploadJob.Id);
                            }
                            else
                            {
                                uploadJob.Status = YouTubeUploadStatus.Failed;
                                uploadJob.LastError = "Render işlemi tamamlanamadı veya çıktı dosyası bulunamadı.";
                                await uploadRepo.UpdateAsync(uploadJob, stoppingToken);
                            }
                        }
                    }
                    catch (Exception uploadLinkEx)
                    {
                        _logger.LogError(uploadLinkEx, "Failed linking completed RenderJob ID {JobId} to YouTubeUploadJob", jobId);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("VideoJob ID {JobId} processing was cancelled.", jobId);
                    await repo.UpdateStatusAsync(jobId, VideoJobStatus.Cancelled, "Render job was cancelled by user.", stoppingToken);

                    try
                    {
                        var uploadRepo = scope.ServiceProvider.GetRequiredService<IYouTubeUploadRepository>();
                        var uploadJob = await uploadRepo.GetByRenderJobIdAsync(jobId, stoppingToken);
                        if (uploadJob != null && uploadJob.Status == YouTubeUploadStatus.WaitingForRender)
                        {
                            await uploadRepo.UpdateStatusAsync(uploadJob.Id, YouTubeUploadStatus.Failed, "Render işlemi iptal edildi.", stoppingToken);
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing VideoJob ID {JobId}", jobId);
                    await repo.UpdateStatusAsync(jobId, VideoJobStatus.Failed, ex.Message, stoppingToken);

                    try
                    {
                        var uploadRepo = scope.ServiceProvider.GetRequiredService<IYouTubeUploadRepository>();
                        var uploadJob = await uploadRepo.GetByRenderJobIdAsync(jobId, stoppingToken);
                        if (uploadJob != null && uploadJob.Status == YouTubeUploadStatus.WaitingForRender)
                        {
                            await uploadRepo.UpdateStatusAsync(uploadJob.Id, YouTubeUploadStatus.Failed, $"Render başarısız: {ex.Message}", stoppingToken);
                        }
                    }
                    catch { }

                    _notificationService.ShowNotification(
                        "❌ Video Render Başarısız",
                        $"'{job.Title}' oluşturulurken hata oluştu: {ex.Message}",
                        "Warning");
                }
                finally
                {
                    _jobQueue.UnregisterActiveJob(jobId);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in VideoJobQueueWorker loop.");
                await Task.Delay(2000, stoppingToken);
            }
        }

        _logger.LogInformation("VideoJobQueueWorker stopping.");
    }
}
