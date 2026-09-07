using System;

namespace KickAutoRecorder.Core.Models;

public class YouTubeAccountInfo
{
    public bool IsConnected { get; set; }
    public string ChannelTitle { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string? ChannelThumbnailUrl { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}
