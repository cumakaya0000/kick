using System;

namespace KickAutoRecorder.Core.Models;

public class FFprobeMetadata
{
    public bool IsValid { get; set; }
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public long Bitrate { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
}
