namespace KickAutoRecorder.Core.Enums;

public enum VideoJobStatus
{
    Pending = 0,
    Queued = 1,
    Preparing = 2,
    Rendering = 3,
    Validating = 4,
    Completed = 5,
    Cancelled = 6,
    Failed = 7,
    Corrupted = 8,
    Interrupted = 9
}
