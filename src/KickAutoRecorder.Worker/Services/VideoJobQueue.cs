using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Worker.Services;

public class VideoJobQueue : IVideoJobQueue
{
    private readonly Channel<int> _queue = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeJobs = new();

    public ValueTask EnqueueAsync(int videoJobId, CancellationToken cancellationToken = default)
    {
        return _queue.Writer.WriteAsync(videoJobId, cancellationToken);
    }

    public ValueTask<int> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _queue.Reader.ReadAsync(cancellationToken);
    }

    public void RegisterActiveJob(int videoJobId, CancellationTokenSource cts)
    {
        _activeJobs[videoJobId] = cts;
    }

    public void UnregisterActiveJob(int videoJobId)
    {
        _activeJobs.TryRemove(videoJobId, out _);
    }

    public bool TryCancel(int videoJobId)
    {
        if (_activeJobs.TryGetValue(videoJobId, out var cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }

    public bool IsProcessing(int videoJobId)
    {
        return _activeJobs.ContainsKey(videoJobId);
    }
}
