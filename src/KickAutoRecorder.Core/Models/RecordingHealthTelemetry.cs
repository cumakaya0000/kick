using System;

namespace KickAutoRecorder.Core.Models;

public class RecordingHealthTelemetry
{
    public int RecordingLogId { get; set; }
    public string StreamerUsername { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime RecordingStartedAt { get; set; } = DateTime.Now;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public long FileSizeBytes { get; set; }
    public double BitrateMbps { get; set; }
    public int SegmentCount { get; set; } = 1;
    public int ReconnectCount { get; set; }
    public bool IsRecovering { get; set; }
    public string? LastError { get; set; }
    public int NextRetrySeconds { get; set; }
    public string FFmpegStatus { get; set; } = "Idle";
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}
