using System;
using System.Collections.Generic;

namespace KickAutoRecorder.Core.Models;

public class SegmentItem
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long FileSizeBytes { get; set; }
    public double DurationSeconds { get; set; }
}

public class SegmentManifest
{
    public int RecordingLogId { get; set; }
    public string StreamerUsername { get; set; } = string.Empty;
    public string StreamTitle { get; set; } = string.Empty;
    public DateTime SessionStartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SessionEndedAt { get; set; }
    public List<SegmentItem> Segments { get; set; } = new();
}
