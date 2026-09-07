using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;

namespace KickAutoRecorder.Core.Interfaces;

public interface IVideoJobRepository
{
    Task<VideoJob?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<VideoJob>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<VideoJob>> GetPendingOrProcessingJobsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(VideoJob job, CancellationToken cancellationToken = default);
    Task UpdateAsync(VideoJob job, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int id, VideoJobStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
