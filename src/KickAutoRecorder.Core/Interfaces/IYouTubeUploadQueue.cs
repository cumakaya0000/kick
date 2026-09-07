using System.Threading;
using System.Threading.Tasks;

namespace KickAutoRecorder.Core.Interfaces;

public interface IYouTubeUploadQueue
{
    ValueTask EnqueueAsync(int uploadJobId, CancellationToken cancellationToken = default);
    ValueTask<int> DequeueAsync(CancellationToken cancellationToken = default);
    bool TryCancel(int uploadJobId);
    void RegisterActiveJob(int uploadJobId, CancellationTokenSource cts);
    void UnregisterActiveJob(int uploadJobId);
}
