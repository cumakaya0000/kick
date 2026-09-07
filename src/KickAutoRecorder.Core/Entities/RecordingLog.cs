using System;
using KickAutoRecorder.Core.Enums;

namespace KickAutoRecorder.Core.Entities;

public class RecordingLog
{
    public int Id { get; set; }
    public int StreamerId { get; set; }
    public string StreamerUsername { get; set; } = string.Empty;
    public string StreamTitle { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public RecordingStatus Status { get; set; } = RecordingStatus.Pending;
    public bool IsValidated { get; set; }
    public string? VideoCodec { get; set; }
    public string? AudioCodec { get; set; }
    public string? Resolution { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ProcessId { get; set; }

    public Streamer? Streamer { get; set; }
}
