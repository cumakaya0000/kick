using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Entities;

namespace KickAutoRecorder.Core.Interfaces;

public interface IYouTubeMetadataDraftRepository
{
    Task<YouTubeVideoDraft?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<YouTubeVideoDraft?> GetByRecordingLogIdAsync(int recordingLogId, CancellationToken cancellationToken = default);
    Task<List<YouTubeVideoDraft>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(YouTubeVideoDraft draft, CancellationToken cancellationToken = default);
    Task UpdateAsync(YouTubeVideoDraft draft, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(YouTubeVideoDraft draft, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
