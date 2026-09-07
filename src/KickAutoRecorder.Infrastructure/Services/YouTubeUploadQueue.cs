using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Infrastructure.Services;

public class YouTubeUploadQueue : IYouTubeUploadQueue
{
    private readonly Channel<int> _queue;
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeJobs = new();

    public YouTubeUploadQueue()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        };
        _queue = Channel.CreateUnbounded<int>(options);
    }

    public ValueTask EnqueueAsync(int uploadJobId, CancellationToken cancellationToken = default)
    {
        return _queue.Writer.WriteAsync(uploadJobId, cancellationToken);
    }

    public ValueTask<int> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _queue.Reader.ReadAsync(cancellationToken);
    }

    public bool TryCancel(int uploadJobId)
    {
        if (_activeJobs.TryRemove(uploadJobId, out var cts))
        {
            try
            {
                cts.Cancel();
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    public void RegisterActiveJob(int uploadJobId, CancellationTokenSource cts)
    {
        _activeJobs[uploadJobId] = cts;
    }

    public void UnregisterActiveJob(int uploadJobId)
    {
        _activeJobs.TryRemove(uploadJobId, out _);
    }
}
