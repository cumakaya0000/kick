using System;

namespace KickAutoRecorder.Core.Models;

public class VideoRenderProgress
{
    public int JobId { get; set; }
    public double ProgressPercentage { get; set; }
    public TimeSpan ProcessedDuration { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public double Fps { get; set; }
    public double BitrateMbps { get; set; }
    public string StatusText { get; set; } = string.Empty;
}
