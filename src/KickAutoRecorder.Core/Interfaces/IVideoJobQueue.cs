using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Entities;

namespace KickAutoRecorder.Core.Interfaces;

public interface IVideoJobQueue
{
    ValueTask EnqueueAsync(int videoJobId, CancellationToken cancellationToken = default);
    ValueTask<int> DequeueAsync(CancellationToken cancellationToken = default);
    bool TryCancel(int videoJobId);
    bool IsProcessing(int videoJobId);
}
