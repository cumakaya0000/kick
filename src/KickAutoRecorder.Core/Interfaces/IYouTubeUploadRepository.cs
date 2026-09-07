using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;

namespace KickAutoRecorder.Core.Interfaces;

public interface IYouTubeUploadRepository
{
    Task<IEnumerable<YouTubeUploadJob>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<YouTubeUploadJob?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<YouTubeUploadJob?> GetByRenderJobIdAsync(int renderJobId, CancellationToken cancellationToken = default);
    Task<IEnumerable<YouTubeUploadJob>> GetPendingOrUploadingJobsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(YouTubeUploadJob job, CancellationToken cancellationToken = default);
    Task UpdateAsync(YouTubeUploadJob job, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int id, YouTubeUploadStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task UpdateProgressAsync(int id, double progressPercent, long uploadedBytes, long totalBytes, string? sessionUri = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
