namespace KickAutoRecorder.Core.Enums;

public enum YouTubeUploadStatus
{
    Pending = 0,
    WaitingForRender = 1,
    Uploading = 2,
    Processing = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6,
    RetryScheduled = 7
}
