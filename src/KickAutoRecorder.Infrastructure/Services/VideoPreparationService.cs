using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Infrastructure.Services;

public class VideoPreparationService : IVideoPreparationService
{
    private readonly IRecordingLogRepository _logRepository;
    private readonly IRecordingEngine _recordingEngine;
    private readonly IFFprobeValidator _ffprobeValidator;
    private readonly ILogger<VideoPreparationService> _logger;

    public VideoPreparationService(
        IRecordingLogRepository logRepository,
        IRecordingEngine recordingEngine,
        IFFprobeValidator ffprobeValidator,
        ILogger<VideoPreparationService> logger)
    {
        _logRepository = logRepository;
        _recordingEngine = recordingEngine;
        _ffprobeValidator = ffprobeValidator;
        _logger = logger;
    }

    public async Task<VideoPreparationResult> PrepareSegmentsAsync(int recordingLogId, CancellationToken cancellationToken = default)
    {
        var result = new VideoPreparationResult();
        var log = await _logRepository.GetByIdAsync(recordingLogId, cancellationToken);
        if (log == null)
        {
            result.Success = false;
            result.ErrorMessage = $"Recording log with ID {recordingLogId} was not found.";
            return result;
        }

        if (string.IsNullOrWhiteSpace(log.FilePath))
        {
            result.Success = false;
            result.ErrorMessage = "Recording log file path is empty.";
            return result;
        }

        var sessionDirectory = Path.GetDirectoryName(log.FilePath);
        if (string.IsNullOrEmpty(sessionDirectory) || !Directory.Exists(sessionDirectory))
        {
            result.Success = false;
            result.ErrorMessage = $"Session directory '{sessionDirectory}' does not exist on disk.";
            return result;
        }

        // Get active file path if streamer is currently recording to exclude active live segment
        string? activeFilePath = null;
        if (_recordingEngine.IsRecording(log.StreamerId))
        {
            var telemetry = _recordingEngine.GetActiveTelemetry(log.StreamerId);
            if (telemetry != null && telemetry.RecordingLogId == recordingLogId)
            {
                // In FFmpegRecordingEngine, segment pattern generates files like segment_001.mp4, segment_002.mp4
                // The last modified mp4 file while active recording is running is the active segment.
                var activeFiles = Directory.GetFiles(sessionDirectory, "*.mp4")
                    .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                    .ToList();
                if (activeFiles.Count > 0)
                {
                    activeFilePath = activeFiles.First();
                    _logger.LogInformation("Excluding currently active segment from render preparation: {ActivePath}", activeFilePath);
                }
            }
        }

        List<string> candidateSegments = new();

        // 1. Try manifest.json first
        var manifestPath = Path.Combine(sessionDirectory, "manifest.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                var manifest = JsonSerializer.Deserialize<SegmentManifest>(json);
                if (manifest != null && manifest.Segments.Count > 0)
                {
                    candidateSegments = manifest.Segments
                        .Select(s => s.FilePath)
                        .Where(p => File.Exists(p) && new FileInfo(p).Length > 0)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read manifest.json in {Folder}. Falling back to directory scan.", sessionDirectory);
            }
        }

        // 2. Fallback to directory scan if manifest didn't produce files
        if (candidateSegments.Count == 0)
        {
            candidateSegments = Directory.GetFiles(sessionDirectory, "segment_*.mp4")
                .Where(p => new FileInfo(p).Length > 0)
                .OrderBy(p => p)
                .ToList();

            if (candidateSegments.Count == 0 && File.Exists(log.FilePath))
            {
                candidateSegments.Add(log.FilePath);
            }
        }

        // Filter out active file
        if (!string.IsNullOrEmpty(activeFilePath))
        {
            candidateSegments = candidateSegments
                .Where(p => !string.Equals(Path.GetFullPath(p), Path.GetFullPath(activeFilePath), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (candidateSegments.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "No completed/closed segment files were found for this recording session.";
            return result;
        }

        result.ClosedSegmentPaths = candidateSegments;

        // Inspect first segment with FFprobe to check for native 4K resolution
        var firstSegment = candidateSegments.First();
        var probe = await _ffprobeValidator.ValidateAndExtractMetadataAsync(firstSegment, cancellationToken);
        if (probe.IsValid)
        {
            result.IsSource4K = (probe.Width >= 3840 || probe.Height >= 2160);
        }

        result.Success = true;
        return result;
    }
}
