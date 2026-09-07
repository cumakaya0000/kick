using System;
using KickAutoRecorder.Core.Enums;

namespace KickAutoRecorder.Core.Entities;

public class Streamer
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string ChannelUrl { get; set; } = string.Empty;
    public bool IsTrackingEnabled { get; set; } = true;
    public StreamStatus CurrentStatus { get; set; } = StreamStatus.Unknown;
    public string StreamTitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ViewerCount { get; set; }
    public string PlaybackUrl { get; set; } = string.Empty;
    public string PreferredQuality { get; set; } = "En Yüksek (Kaynak)";
    public DateTime? LastCheckedAt { get; set; }
    public DateTime? LastLiveAt { get; set; }
    public DateTime? LastOfflineAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
