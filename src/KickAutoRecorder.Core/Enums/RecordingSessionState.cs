namespace KickAutoRecorder.Core.Enums;

public enum RecordingSessionState
{
    Idle,
    Starting,
    Recording,
    Recovering,
    Stopping,
    Completed,
    Failed,
    Interrupted
}
