using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Recording.Services;

public class FFmpegVideoRenderEngine : IVideoRenderEngine
{
    private readonly IFFmpegLocator _ffmpegLocator;
    private readonly IFFprobeValidator _ffprobeValidator;
    private readonly IDiskService _diskService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FFmpegVideoRenderEngine> _logger;

    public event Action<VideoRenderProgress>? OnRenderProgress;

    public FFmpegVideoRenderEngine(
        IFFmpegLocator ffmpegLocator,
        IFFprobeValidator ffprobeValidator,
        IDiskService diskService,
        IServiceScopeFactory scopeFactory,
        ILogger<FFmpegVideoRenderEngine> logger)
    {
        _ffmpegLocator = ffmpegLocator;
        _ffprobeValidator = ffprobeValidator;
        _diskService = diskService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteRenderJobAsync(VideoJob job, CancellationToken cancellationToken = default)
    {
        if (job == null) throw new ArgumentNullException(nameof(job));

        job.Status = VideoJobStatus.Preparing;

        var ffmpegExe = _ffmpegLocator.FindFFmpegExecutable();
        if (string.IsNullOrEmpty(ffmpegExe))
        {
            throw new FileNotFoundException("FFmpeg executable (ffmpeg.exe) was not found. Please verify Settings.");
        }

        List<string> segmentPaths = new();
        try
        {
            segmentPaths = JsonSerializer.Deserialize<List<string>>(job.SegmentPathsJson) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse SegmentPathsJson for VideoJob {JobId}", job.Id);
        }

        var validSegments = segmentPaths.Where(p => File.Exists(p) && new FileInfo(p).Length > 0).ToList();
        if (validSegments.Count == 0)
        {
            throw new InvalidOperationException("No valid segment files exist on disk for this render job.");
        }

        // Validate trim start/end timestamps
        if (job.TrimStartTimeSeconds.HasValue && job.TrimEndTimeSeconds.HasValue)
        {
            if (job.TrimStartTimeSeconds.Value >= job.TrimEndTimeSeconds.Value)
            {
                throw new InvalidOperationException("Invalid trim boundaries: Start time cannot be greater than or equal to end time.");
            }
        }

        // Strictly prevent writing over source file
        foreach (var seg in validSegments)
        {
            if (string.Equals(Path.GetFullPath(seg), Path.GetFullPath(job.OutputPath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Output file path cannot be identical to source video file path.");
            }
        }

        var outputFolder = Path.GetDirectoryName(job.OutputPath);
        if (!string.IsNullOrEmpty(outputFolder))
        {
            _diskService.EnsureDirectoryExists(outputFolder);
        }

        // Calculate total expected duration
        TimeSpan totalExpectedDuration = TimeSpan.Zero;
        foreach (var seg in validSegments)
        {
            var meta = await _ffprobeValidator.ValidateAndExtractMetadataAsync(seg, cancellationToken);
            if (!meta.IsValid)
            {
                _logger.LogWarning("Source segment file {Segment} failed FFprobe pre-validation.", seg);
            }
            if (meta.Duration.TotalSeconds > 0)
            {
                totalExpectedDuration += meta.Duration;
            }
        }

        if (job.TrimEndTimeSeconds.HasValue && job.TrimStartTimeSeconds.HasValue)
        {
            var trimmedSec = job.TrimEndTimeSeconds.Value - job.TrimStartTimeSeconds.Value;
            if (trimmedSec > 0)
            {
                totalExpectedDuration = TimeSpan.FromSeconds(trimmedSec);
            }
        }

        // Pre-flight disk check (estimate required free space = total segment sizes * 1.2)
        var totalSourceSize = validSegments.Sum(s => new FileInfo(s).Length);
        var requiredFreeBytes = (long)(totalSourceSize * 1.2);
        if (!string.IsNullOrEmpty(outputFolder) && !_diskService.HasSufficientSpace(outputFolder, requiredFreeBytes))
        {
            var freeBytes = _diskService.GetAvailableFreeSpaceBytes(outputFolder);
            throw new InvalidOperationException($"Insufficient disk space for rendering. Free: {freeBytes / 1e9:N2} GB, Required: {requiredFreeBytes / 1e9:N2} GB.");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "KickAutoRecorder_Render", job.Id.ToString());
        _diskService.EnsureDirectoryExists(tempDirectory);

        var concatTxtPath = Path.Combine(tempDirectory, "concat.txt");
        var tempOutputPath = job.OutputPath + ".tmp.mp4";

        try
        {
            var concatLines = validSegments.Select(p => $"file '{p.Replace("\\", "/")}'");
            await File.WriteAllLinesAsync(concatTxtPath, concatLines, cancellationToken);

            var isFastCopyRequested = string.Equals(job.ExportPreset, "FastCopy", StringComparison.OrdinalIgnoreCase);

            job.Status = VideoJobStatus.Rendering;
            bool renderSuccess = false;

            if (isFastCopyRequested)
            {
                _logger.LogInformation("Executing FAST MODE (Stream Copy) for VideoJob {JobId}", job.Id);
                var fastCopyArgs = BuildFFmpegArguments(concatTxtPath, tempOutputPath, isFastCopy: true, job);

                renderSuccess = await RunFFmpegProcessAsync(ffmpegExe, fastCopyArgs, job, totalExpectedDuration, tempOutputPath, cancellationToken);

                if (renderSuccess && File.Exists(tempOutputPath))
                {
                    var validation = await _ffprobeValidator.ValidateAndExtractMetadataAsync(tempOutputPath, cancellationToken);
                    if (!validation.IsValid)
                    {
                        _logger.LogWarning("FAST MODE result validation failed ({Error}). Falling back to ACCURATE MODE (Re-encode)...", validation.ErrorMessage);
                        renderSuccess = false;
                        if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);
                    }
                }
            }

            if (!renderSuccess)
            {
                _logger.LogInformation("Executing ACCURATE MODE (Re-encode) for VideoJob {JobId}", job.Id);
                var transcodeArgs = BuildFFmpegArguments(concatTxtPath, tempOutputPath, isFastCopy: false, job);

                renderSuccess = await RunFFmpegProcessAsync(ffmpegExe, transcodeArgs, job, totalExpectedDuration, tempOutputPath, cancellationToken);
            }

            if (!renderSuccess || !File.Exists(tempOutputPath))
            {
                throw new InvalidOperationException("FFmpeg rendering process failed or produced no output file.");
            }

            // Validating phase
            job.Status = VideoJobStatus.Validating;
            var finalValidation = await _ffprobeValidator.ValidateAndExtractMetadataAsync(tempOutputPath, cancellationToken);
            if (!finalValidation.IsValid)
            {
                job.Status = VideoJobStatus.Corrupted;
                throw new InvalidOperationException($"Rendered output validation failed: {finalValidation.ErrorMessage}");
            }

            // Atomic rename of .tmp.mp4 to final .mp4
            if (File.Exists(job.OutputPath))
            {
                File.Delete(job.OutputPath);
            }
            File.Move(tempOutputPath, job.OutputPath);

            job.FileSizeBytes = new FileInfo(job.OutputPath).Length;
            job.ProgressPercentage = 100.0;
            job.Status = VideoJobStatus.Completed;

            _logger.LogInformation("Successfully completed VideoJob {JobId}. Output saved to {OutputPath} ({SizeGB:N2} GB)",
                job.Id, job.OutputPath, job.FileSizeBytes / 1e9);
        }
        finally
        {
            try
            {
                if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temp render directory {TempDir}", tempDirectory);
            }
        }
    }

    private List<string> BuildFFmpegArguments(string concatTxtPath, string tempOutputPath, bool isFastCopy, VideoJob job)
    {
        var args = new List<string> { "-y" };

        if (job.TrimStartTimeSeconds.HasValue && job.TrimStartTimeSeconds.Value > 0)
        {
            args.Add("-ss");
            args.Add(TimeSpan.FromSeconds(job.TrimStartTimeSeconds.Value).ToString("hh\\:mm\\:ss\\.fff"));
        }

        args.AddRange(new[] { "-f", "concat", "-safe", "0", "-i", concatTxtPath });

        if (job.TrimEndTimeSeconds.HasValue && job.TrimEndTimeSeconds.Value > 0)
        {
            args.Add("-to");
            args.Add(TimeSpan.FromSeconds(job.TrimEndTimeSeconds.Value).ToString("hh\\:mm\\:ss\\.fff"));
        }

        if (isFastCopy)
        {
            args.AddRange(new[] { "-c", "copy", "-movflags", "+faststart", tempOutputPath });
            return args;
        }
        else
        {
            var audioBitrate = job.AudioBitrateKbps > 0 ? job.AudioBitrateKbps : 256;

            if (!string.IsNullOrWhiteSpace(job.TargetResolution))
            {
                var res = job.TargetResolution.ToLowerInvariant().Replace("x", ":");
                args.Add("-vf");
                
                if (job.TargetResolution == "1080x1920")
                {
                    // Scale to fit height, crop width to center (Standard Shorts format)
                    args.Add("scale=-1:1920,crop=1080:1920");
                }
                else
                {
                    args.Add($"scale={res}");
                }
            }

            if (job.TargetFps.HasValue && job.TargetFps.Value > 0)
            {
                args.Add("-r");
                args.Add(job.TargetFps.Value.ToString());
            }

            args.AddRange(new[]
            {
                "-c:v", "libx264",
                "-profile:v", "high",
                "-level:v", "4.2",
                "-pix_fmt", "yuv420p",
                "-preset", "medium",
                "-crf", "16",
                "-bf", "2",
                "-g", "120",
                "-c:a", "aac",
                "-b:a", $"{audioBitrate}k",
                "-ar", "48000",
                "-movflags", "+faststart",
                tempOutputPath
            });

            return args;
        }
    }

    private async Task<bool> RunFFmpegProcessAsync(
        string ffmpegExe,
        List<string> arguments,
        VideoJob job,
        TimeSpan totalExpectedDuration,
        string tempOutputPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegExe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                ParseRenderProgressLine(args.Data, job, totalExpectedDuration);
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        try
        {
            process.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch { }

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 && File.Exists(tempOutputPath) && new FileInfo(tempOutputPath).Length > 0;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("VideoJob {JobId} rendering was canceled by user.", job.Id);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            throw;
        }
    }

    private void ParseRenderProgressLine(string line, VideoJob job, TimeSpan totalExpectedDuration)
    {
        try
        {
            var timeMatch = Regex.Match(line, @"time=(\d{2}):(\d{2}):(\d{2}\.\d+)");
            if (timeMatch.Success)
            {
                if (double.TryParse(timeMatch.Groups[1].Value, out var hours) &&
                    double.TryParse(timeMatch.Groups[2].Value, out var mins) &&
                    double.TryParse(timeMatch.Groups[3].Value, out var secs))
                {
                    var processed = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(mins) + TimeSpan.FromSeconds(secs);
                    var percentage = 0.0;
                    if (totalExpectedDuration.TotalSeconds > 0)
                    {
                        percentage = Math.Min(99.9, (processed.TotalSeconds / totalExpectedDuration.TotalSeconds) * 100.0);
                    }

                    job.ProgressPercentage = Math.Round(percentage, 1);

                    var fpsMatch = Regex.Match(line, @"fps=\s*([\d\.]+)");
                    double fps = 0;
                    if (fpsMatch.Success) double.TryParse(fpsMatch.Groups[1].Value, out fps);

                    var bitrateMatch = Regex.Match(line, @"bitrate=\s*([\d\.]+)kbits/s");
                    double bitrateKbps = 0;
                    if (bitrateMatch.Success) double.TryParse(bitrateMatch.Groups[1].Value, out bitrateKbps);

                    var speedMatch = Regex.Match(line, @"speed=\s*([\d\.]+)x");
                    double speed = 1.0;
                    if (speedMatch.Success) double.TryParse(speedMatch.Groups[1].Value, out speed);

                    TimeSpan? eta = null;
                    if (speed > 0 && totalExpectedDuration.TotalSeconds > processed.TotalSeconds)
                    {
                        var remainingSec = (totalExpectedDuration.TotalSeconds - processed.TotalSeconds) / speed;
                        if (remainingSec > 0) eta = TimeSpan.FromSeconds(remainingSec);
                    }

                    var progress = new VideoRenderProgress
                    {
                        JobId = job.Id,
                        ProgressPercentage = job.ProgressPercentage,
                        ProcessedDuration = processed,
                        TotalDuration = totalExpectedDuration,
                        EstimatedTimeRemaining = eta,
                        Fps = fps,
                        BitrateMbps = bitrateKbps / 1000.0,
                        StatusText = $"Rendering ({job.ProgressPercentage:N1}%)"
                    };

                    OnRenderProgress?.Invoke(progress);
                }
            }
        }
        catch
        {
            // Ignore progress parsing errors
        }
    }
}
