using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Core.Interfaces;

public class VideoPreparationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ClosedSegmentPaths { get; set; } = new();
    public double TotalDurationSeconds { get; set; }
    public bool IsSource4K { get; set; }
}

public interface IVideoPreparationService
{
    /// <summary>
    /// Inspects manifest.json and session folder for a recording log, excluding any currently active live segment.
    /// Returns ordered list of finalized segment files and stream metadata.
    /// </summary>
    Task<VideoPreparationResult> PrepareSegmentsAsync(int recordingLogId, CancellationToken cancellationToken = default);
}
