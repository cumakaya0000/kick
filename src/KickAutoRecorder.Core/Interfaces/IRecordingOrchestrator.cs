using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Entities;

namespace KickAutoRecorder.Core.Interfaces;

public interface IRecordingOrchestrator
{
    Task ProcessStreamerCheckAsync(Streamer streamer, CancellationToken cancellationToken = default);
    Task RecoverUnfinishedRecordingsAsync(CancellationToken cancellationToken = default);
    Task StopAllActiveRecordingsAsync(CancellationToken cancellationToken = default);
    void NotifyManualStop(int streamerId);
}
