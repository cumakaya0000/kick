using System;
using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Core.Interfaces;

public interface IYouTubeUploadService
{
    YouTubeUploadValidationResult ValidateJobBeforeUpload(YouTubeUploadJob job, bool checkAuth = true);
    Task<YouTubeUploadJob> ExecuteUploadJobAsync(
        YouTubeUploadJob job,
        Action<double, long, long>? onProgress = null,
        CancellationToken cancellationToken = default);
}
