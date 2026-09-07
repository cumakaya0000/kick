using System;

namespace KickAutoRecorder.Core.Entities;

public class YouTubeVideoDraft
{
    public int Id { get; set; }
    public int RecordingLogId { get; set; }
    public int? VideoJobId { get; set; }

    public string StreamerUsername { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Category { get; set; } = "Gaming";
    public string Language { get; set; } = "Turkish";
    public string? GameName { get; set; }
    public string? ThumbnailPath { get; set; }
    public string PrivacyStatus { get; set; } = "Private"; // Private, Unlisted, Public
    public string? PlaylistName { get; set; }
    public bool MadeForKids { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public RecordingLog? RecordingLog { get; set; }
    public VideoJob? VideoJob { get; set; }
}
