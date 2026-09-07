using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Entities;

namespace KickAutoRecorder.Core.Interfaces;

public record AddStreamerResult(bool IsSuccess, Streamer? Streamer, string? ErrorMessage);

public interface IStreamerService
{
    Task<IEnumerable<Streamer>> GetAllStreamersAsync(CancellationToken cancellationToken = default);
    Task<AddStreamerResult> AddStreamerAsync(string usernameOrUrl, CancellationToken cancellationToken = default);
    Task<bool> ToggleTrackingAsync(int streamerId, CancellationToken cancellationToken = default);
    Task<bool> DeleteStreamerAsync(int streamerId, CancellationToken cancellationToken = default);
    Task<Streamer?> CheckAndUpdateStreamerStatusAsync(int streamerId, CancellationToken cancellationToken = default);
    Task<bool> UpdatePreferredQualityAsync(int streamerId, string quality, CancellationToken cancellationToken = default);
    Task CheckAndUpdateAllStatusesAsync(CancellationToken cancellationToken = default);
    (string NormalizedUsername, string ChannelUrl) NormalizeStreamerInput(string input);
}
