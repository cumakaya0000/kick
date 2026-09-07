using System;
using KickAutoRecorder.Core.Enums;

namespace KickAutoRecorder.Core.Entities;

public class YouTubeUploadJob
{
    public int Id { get; set; }
    public int? RenderJobId { get; set; }
    public int? RecordingLogId { get; set; }
    public string VideoFilePath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public string CategoryId { get; set; } = "20"; // 20 = Gaming
    public string DefaultLanguage { get; set; } = "tr";
    public string PrivacyStatus { get; set; } = "Private"; // Default SAFE Privacy
    public bool MadeForKids { get; set; }
    public string? PlaylistId { get; set; }

    public YouTubeUploadStatus Status { get; set; } = YouTubeUploadStatus.Pending;
    public double ProgressPercent { get; set; }

    // Advanced Resumable Upload Session Tracking
    public string? UploadSessionUri { get; set; }
    public long UploadedBytes { get; set; }
    public long TotalBytes { get; set; }
    public DateTime? UploadSessionCreatedAt { get; set; }
    public DateTime? LastProgressAt { get; set; }

    public string? YouTubeVideoId { get; set; }
    public string? YouTubeVideoUrl { get; set; }

    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public string? ThumbnailWarning { get; set; }
    public string? PlaylistWarning { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation Properties
    public RecordingLog? RecordingLog { get; set; }
    public VideoJob? RenderJob { get; set; }
}
