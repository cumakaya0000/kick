using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Enums;

namespace KickAutoRecorder.Core.Interfaces;

public record KickChannelStatusResult(
    string Username,
    StreamStatus Status,
    string StreamTitle,
    string PlaybackUrl,
    int ViewerCount,
    string Category,
    string? ErrorMessage = null
);

public interface IKickApiClient
{
    Task<KickChannelStatusResult> CheckChannelStatusAsync(string username, CancellationToken cancellationToken = default);
}
