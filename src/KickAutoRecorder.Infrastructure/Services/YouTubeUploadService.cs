using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Google;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Infrastructure.Services;

public class YouTubeUploadService : IYouTubeUploadService
{
    private readonly IYouTubeAuthService _authService;
    private readonly IYouTubeMetadataMapper _metadataMapper;
    private readonly ILogger<YouTubeUploadService> _logger;

    public YouTubeUploadService(
        IYouTubeAuthService authService,
        IYouTubeMetadataMapper metadataMapper,
        ILogger<YouTubeUploadService> logger)
    {
        _authService = authService;
        _metadataMapper = metadataMapper;
        _logger = logger;
    }

    public async Task<YouTubeUploadValidationResult> ValidateJobBeforeUploadAsync(YouTubeUploadJob job, bool checkAuth = true, CancellationToken cancellationToken = default)
    {
        var result = new YouTubeUploadValidationResult();

        if (string.IsNullOrWhiteSpace(job.VideoFilePath))
        {
            result.Errors.Add("Video dosya yolu boş.");
        }
        else if (!File.Exists(job.VideoFilePath))
        {
            result.Errors.Add($"Video dosyası diskte bulunamadı: '{Path.GetFileName(job.VideoFilePath)}'");
        }
        else
        {
            var fileInfo = new FileInfo(job.VideoFilePath);
            if (fileInfo.Length == 0)
            {
                result.Errors.Add("Video dosya boyutu 0 bayt.");
            }
        }

        if (string.IsNullOrWhiteSpace(job.Title))
        {
            result.Errors.Add("Video başlığı boş olamaz.");
        }
        else if (job.Title.Length > 100)
        {
            result.Errors.Add("Video başlığı 100 karakter sınırını aşıyor.");
        }

        if (!string.IsNullOrEmpty(job.Description) && job.Description.Length > 5000)
        {
            result.Errors.Add("Video açıklaması 5000 karakter sınırını aşıyor.");
        }

        if (checkAuth)
        {
            var account = await _authService.GetAccountInfoAsync(cancellationToken);
            if (!account.IsConnected)
            {
                result.Errors.Add("YouTube hesabı bağlı değil. Lütfen Ayarlar sayfasından hesabınızı bağlayın.");
            }
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    public async Task<YouTubeUploadJob> ExecuteUploadJobAsync(
        YouTubeUploadJob job,
        Action<double, long, long>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateJobBeforeUploadAsync(job, checkAuth: true, cancellationToken);
        if (!validation.IsValid)
        {
            job.Status = YouTubeUploadStatus.Failed;
            job.LastError = validation.ErrorSummary;
            return job;
        }

        // Idempotency: If YouTubeVideoId is already present, video was already uploaded!
        if (!string.IsNullOrEmpty(job.YouTubeVideoId) && job.Status == YouTubeUploadStatus.Completed)
        {
            _logger.LogInformation("Job ID {JobId} already has YouTubeVideoId {VideoId}. Skipping video upload.", job.Id, job.YouTubeVideoId);
            return job;
        }

        var youtubeService = await _authService.GetYouTubeServiceAsync(cancellationToken);
        if (youtubeService == null)
        {
            job.Status = YouTubeUploadStatus.Failed;
            job.LastError = "YouTube servisine erişilemedi. Oturum süresi dolmuş olabilir.";
            return job;
        }

        // 1. Prepare Video Metadata
        var video = new Video
        {
            Snippet = new VideoSnippet
            {
                Title = _metadataMapper.SanitizeTitle(job.Title),
                Description = job.Description,
                Tags = _metadataMapper.ParseTags(job.Tags),
                CategoryId = job.CategoryId ?? "20",
                DefaultLanguage = job.DefaultLanguage ?? "tr"
            },
            Status = new VideoStatus
            {
                // Strict Safe Default Privacy Enforcement
                PrivacyStatus = string.Equals(job.PrivacyStatus, "Public", StringComparison.OrdinalIgnoreCase) ? "public" :
                                string.Equals(job.PrivacyStatus, "Unlisted", StringComparison.OrdinalIgnoreCase) ? "unlisted" : "private",
                MadeForKids = job.MadeForKids,
                SelfDeclaredMadeForKids = job.MadeForKids
            }
        };

        using var fileStream = new FileStream(job.VideoFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var totalBytes = fileStream.Length;
        job.TotalBytes = totalBytes;
        job.StartedAt ??= DateTime.UtcNow;

        _logger.LogInformation("Starting Resumable YouTube Video Upload for Job ID {JobId}: '{Title}' ({Size:N0} bytes, Privacy: {Privacy})",
            job.Id, job.Title, totalBytes, video.Status.PrivacyStatus);

        var insertRequest = youtubeService.Videos.Insert(video, "snippet,status", fileStream, "video/*");
        insertRequest.ChunkSize = ResumableUpload.MinimumChunkSize * 4; // 1MB chunks

        Video? uploadedVideo = null;
        Exception? uploadException = null;

        insertRequest.ProgressChanged += (IUploadProgress progress) =>
        {
            switch (progress.Status)
            {
                case UploadStatus.Uploading:
                    job.Status = YouTubeUploadStatus.Uploading;
                    job.UploadedBytes = progress.BytesSent;
                    job.TotalBytes = totalBytes;
                    job.ProgressPercent = totalBytes > 0 ? Math.Min(99.0, Math.Round((double)progress.BytesSent / totalBytes * 100.0, 1)) : 0;
                    job.LastProgressAt = DateTime.UtcNow;

                    onProgress?.Invoke(job.ProgressPercent, job.UploadedBytes, job.TotalBytes);
                    break;

                case UploadStatus.Completed:
                    job.UploadedBytes = totalBytes;
                    job.ProgressPercent = 100.0;
                    job.LastProgressAt = DateTime.UtcNow;
                    break;

                case UploadStatus.Failed:
                    uploadException = progress.Exception;
                    break;
            }
        };

        insertRequest.ResponseReceived += (Video v) =>
        {
            uploadedVideo = v;
        };

        try
        {
            job.Status = YouTubeUploadStatus.Uploading;
            await insertRequest.UploadAsync(cancellationToken);

            if (uploadException != null)
            {
                throw uploadException;
            }

            if (uploadedVideo != null && !string.IsNullOrEmpty(uploadedVideo.Id))
            {
                job.YouTubeVideoId = uploadedVideo.Id;
                job.YouTubeVideoUrl = $"https://www.youtube.com/watch?v={uploadedVideo.Id}";
                job.Status = YouTubeUploadStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("✅ YouTube Video Upload Successful! Job ID: {JobId}, Video ID: {VideoId}, URL: {Url}",
                    job.Id, job.YouTubeVideoId, job.YouTubeVideoUrl);
            }
            else
            {
                throw new InvalidOperationException("YouTube API did not return a valid Video ID upon upload completion.");
            }
        }
        catch (GoogleApiException apiEx)
        {
            _logger.LogError(apiEx, "Google API Exception during YouTube Upload for Job ID {JobId}", job.Id);

            // Dynamic Quota Exception Detection via API response
            if (apiEx.Error != null && apiEx.Error.Errors != null &&
                apiEx.Error.Errors.Any(e => string.Equals(e.Reason, "quotaExceeded", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(e.Reason, "rateLimitExceeded", StringComparison.OrdinalIgnoreCase)))
            {
                job.Status = YouTubeUploadStatus.RetryScheduled;
                job.LastError = "YouTube API günlük kota sınırı aşıldı (quotaExceeded). Yükleme ertelendi.";
            }
            else
            {
                job.Status = YouTubeUploadStatus.Failed;
                job.LastError = $"YouTube API Hatası ({apiEx.HttpStatusCode}): {apiEx.Message}";
            }
            return job;
        }
        catch (OperationCanceledException)
        {
            job.Status = YouTubeUploadStatus.Cancelled;
            job.LastError = "Upload was cancelled by user or application shutdown.";
            return job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected exception uploading video Job ID {JobId}", job.Id);
            job.Status = YouTubeUploadStatus.Failed;
            job.LastError = $"Yükleme Hatası: {ex.Message}";
            return job;
        }

        // 2. Thumbnail Upload (Isolated: Failure will NOT mark video upload as failed!)
        if (!string.IsNullOrWhiteSpace(job.ThumbnailPath) && File.Exists(job.ThumbnailPath) && !string.IsNullOrEmpty(job.YouTubeVideoId))
        {
            try
            {
                _logger.LogInformation("Uploading custom thumbnail for Video ID {VideoId}...", job.YouTubeVideoId);
                using var thumbStream = new FileStream(job.ThumbnailPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var contentType = job.ThumbnailPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
                var thumbRequest = youtubeService.Thumbnails.Set(job.YouTubeVideoId, thumbStream, contentType);
                var thumbProgress = await thumbRequest.UploadAsync(cancellationToken);
                if (thumbProgress.Status == UploadStatus.Completed)
                {
                    _logger.LogInformation("✅ Custom Thumbnail uploaded successfully for Video ID {VideoId}", job.YouTubeVideoId);
                }
                else
                {
                    job.ThumbnailWarning = $"Video yüklendi ancak küçük resim (thumbnail) yüklenemedi: {thumbProgress.Exception?.Message}";
                    _logger.LogWarning("Thumbnail upload warning for Video ID {VideoId}: {Warning}", job.YouTubeVideoId, job.ThumbnailWarning);
                }
            }
            catch (Exception thumbEx)
            {
                job.ThumbnailWarning = $"Video yüklendi ancak küçük resim (thumbnail) yüklenemedi: {thumbEx.Message}";
                _logger.LogWarning(thumbEx, "Thumbnail upload failed for Video ID {VideoId}", job.YouTubeVideoId);
            }
        }

        // 3. Playlist Insertion (Isolated: Failure will NOT mark video upload as failed!)
        if (!string.IsNullOrWhiteSpace(job.PlaylistId) && !string.IsNullOrEmpty(job.YouTubeVideoId))
        {
            try
            {
                _logger.LogInformation("Adding Video ID {VideoId} to Playlist ID {PlaylistId}...", job.YouTubeVideoId, job.PlaylistId);
                var playlistItem = new PlaylistItem
                {
                    Snippet = new PlaylistItemSnippet
                    {
                        PlaylistId = job.PlaylistId,
                        ResourceId = new ResourceId
                        {
                            Kind = "youtube#video",
                            VideoId = job.YouTubeVideoId
                        }
                    }
                };

                await youtubeService.PlaylistItems.Insert(playlistItem, "snippet").ExecuteAsync(cancellationToken);
                _logger.LogInformation("✅ Video ID {VideoId} added to Playlist ID {PlaylistId}", job.YouTubeVideoId, job.PlaylistId);
            }
            catch (Exception playlistEx)
            {
                job.PlaylistWarning = $"Video yüklendi ancak oynatma listesine eklenemedi: {playlistEx.Message}";
                _logger.LogWarning(playlistEx, "Playlist insertion failed for Video ID {VideoId}", job.YouTubeVideoId);
            }
        }

        return job;
    }
}
