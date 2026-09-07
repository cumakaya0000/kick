using System;
using KickAutoRecorder.Core.Enums;

namespace KickAutoRecorder.Core.Entities;

public class VideoJob
{
    public int Id { get; set; }
    public int RecordingLogId { get; set; }
    public string StreamerUsername { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Tags { get; set; }

    /// <summary>
    /// JSON array storing paths of closed segment files included in this job.
    /// </summary>
    public string SegmentPathsJson { get; set; } = "[]";

    public double? TrimStartTimeSeconds { get; set; }
    public double? TrimEndTimeSeconds { get; set; }

    public string ExportPreset { get; set; } = "FastCopy"; // "FastCopy", "YouTube1080p60", "YouTube4K"
    public string? TargetResolution { get; set; }
    public int? TargetFps { get; set; }
    public int AudioBitrateKbps { get; set; } = 256;

    public string OutputPath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public long FileSizeBytes { get; set; }

    public VideoJobStatus Status { get; set; } = VideoJobStatus.Pending;
    public double ProgressPercentage { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public RecordingLog? RecordingLog { get; set; }
}
