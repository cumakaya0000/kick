using System;
using System.Collections.Concurrent;
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
using KickAutoRecorder.Recording.Models;

namespace KickAutoRecorder.Recording.Services;

public class FFmpegRecordingEngine : IRecordingEngine, IDisposable
{
    private readonly IFFmpegLocator _ffmpegLocator;
    private readonly IDiskService _diskService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INotificationService _notificationService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<FFmpegRecordingEngine> _logger;

    private readonly ConcurrentDictionary<int, ActiveRecordingContext> _activeRecordings = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _streamerLocks = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _reconnectLocks = new();
    private readonly ConcurrentDictionary<int, RecordingSessionState> _sessionStates = new();

    public event Action<RecordingHealthTelemetry>? OnTelemetryUpdated;

    public FFmpegRecordingEngine(
        IFFmpegLocator ffmpegLocator,
        IDiskService diskService,
        IServiceScopeFactory scopeFactory,
        INotificationService notificationService,
        ISettingsService settingsService,
        ILogger<FFmpegRecordingEngine> logger)
    {
        _ffmpegLocator = ffmpegLocator;
        _diskService = diskService;
        _scopeFactory = scopeFactory;
        _notificationService = notificationService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public bool IsRecording(int streamerId)
    {
        if (_sessionStates.TryGetValue(streamerId, out var state))
        {
            if (state == RecordingSessionState.Starting || state == RecordingSessionState.Recording || state == RecordingSessionState.Recovering)
            {
                return true;
            }
        }
        return _activeRecordings.Values.Any(r =>
        {
            try
            {
                return r.StreamerId == streamerId && !r.Process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        });
    }

    public RecordingHealthTelemetry? GetActiveTelemetry(int streamerId)
    {
        var context = _activeRecordings.Values.FirstOrDefault(r => r.StreamerId == streamerId);
        return context?.Telemetry;
    }

    public async Task<RecordingLog> StartRecordingAsync(
        Streamer streamer, 
        string streamUrl, 
        string outputDirectory, 
        CancellationToken cancellationToken = default)
    {
        if (streamer == null)
            throw new ArgumentNullException(nameof(streamer));

        var streamerLock = _streamerLocks.GetOrAdd(streamer.Id, _ => new SemaphoreSlim(1, 1));
        await streamerLock.WaitAsync(cancellationToken);

        try
        {
            if (_sessionStates.TryGetValue(streamer.Id, out var currentState) &&
                (currentState == RecordingSessionState.Starting || currentState == RecordingSessionState.Recording))
            {
                _logger.LogWarning("Duplicate recording start rejected by Coordinator: Streamer {Username} is already in state {State}.", streamer.Username, currentState);
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IRecordingLogRepository>();
                var activeLogs = await repo.GetActiveRecordingsAsync(cancellationToken);
                var existingLog = activeLogs.FirstOrDefault(r => r.StreamerId == streamer.Id);
                if (existingLog != null) return existingLog;
            }

            _sessionStates[streamer.Id] = RecordingSessionState.Starting;

            var ffmpegPath = _ffmpegLocator.FindFFmpegExecutable();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                _sessionStates[streamer.Id] = RecordingSessionState.Failed;
                _logger.LogError("FFmpeg executable (ffmpeg.exe) was not found.");
                throw new FileNotFoundException("FFmpeg executable (ffmpeg.exe) was not found. Please set its path in Settings.");
            }

            if (!Path.IsPathRooted(outputDirectory))
            {
                outputDirectory = Path.Combine(AppContext.BaseDirectory, outputDirectory);
            }

            var sessionFolder = Path.Combine(outputDirectory, streamer.Username, DateTime.Now.ToString("yyyy-MM-dd"));
            _diskService.EnsureDirectoryExists(sessionFolder);

            var minDiskGB = _settingsService.GetSettings().MinDiskSpaceGB;
            var minDiskBytes = (minDiskGB > 0 ? minDiskGB : 5) * 1024L * 1024L * 1024L;

            if (!_diskService.HasSufficientSpace(sessionFolder, minDiskBytes))
            {
                var freeBytes = _diskService.GetAvailableFreeSpaceBytes(sessionFolder);
                _sessionStates[streamer.Id] = RecordingSessionState.Failed;
                _logger.LogError("Insufficient disk space ({FreeGB:N2} GB < {MinGB} GB limit).", freeBytes / 1e9, minDiskGB);
                throw new InvalidOperationException($"Low disk space ({freeBytes / (1024 * 1024 * 1024):N1} GB available). Minimum {minDiskGB} GB required.");
            }

            var segmentPattern = Path.Combine(sessionFolder, "segment_%03d.mp4");
            var initialFilePath = Path.Combine(sessionFolder, "segment_001.mp4");

            var log = new RecordingLog
            {
                StreamerId = streamer.Id,
                StreamerUsername = streamer.Username,
                StreamTitle = streamer.StreamTitle,
                FilePath = initialFilePath,
                StartedAt = DateTime.Now,
                Status = RecordingStatus.Recording
            };

            using (var scope = _scopeFactory.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IRecordingLogRepository>();
                await repo.AddAsync(log, cancellationToken);
            }

            var quality = !string.IsNullOrWhiteSpace(streamer.PreferredQuality) ? streamer.PreferredQuality : "En Yüksek (Kaynak)";
            var arguments = BuildFFmpegArguments(streamUrl, segmentPattern, quality);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };

            foreach (var arg in arguments)
            {
                processStartInfo.ArgumentList.Add(arg);
            }

            var process = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true };
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var telemetry = new RecordingHealthTelemetry
            {
                RecordingLogId = log.Id,
                StreamerUsername = streamer.Username,
                IsActive = true,
                RecordingStartedAt = DateTime.Now,
                FFmpegStatus = "Running",
                LastUpdatedAt = DateTime.Now
            };

            var context = new ActiveRecordingContext
            {
                RecordingLogId = log.Id,
                StreamerId = streamer.Id,
                StreamerUsername = streamer.Username,
                FilePath = initialFilePath,
                SessionFolder = sessionFolder,
                StartedAt = DateTime.Now,
                Process = process,
                CancellationTokenSource = cts,
                Telemetry = telemetry
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    ParseTelemetryLine(args.Data, context);
                }
            };

            process.Exited += (sender, args) =>
            {
                Task.Run(async () =>
                {
                    _logger.LogInformation("FFmpeg process exited for streamer: {Username} (ExitCode: {ExitCode})", streamer.Username, process.ExitCode);
                    await HandleProcessExitedAsync(log.Id, context, process.ExitCode);
                });
            };

            _activeRecordings.TryAdd(log.Id, context);
            process.Start();
            process.BeginErrorReadLine();
            
            try
            {
                log.ProcessId = process.Id;
                using var pScope = _scopeFactory.CreateScope();
                var pRepo = pScope.ServiceProvider.GetRequiredService<IRecordingLogRepository>();
                await pRepo.UpdateAsync(log, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save ProcessId {Pid} to database for LogId {LogId}", process.Id, log.Id);
            }

            _sessionStates[streamer.Id] = RecordingSessionState.Recording;
            UpdateManifest(context);

            _notificationService.ShowNotification(
                $"🔴 {streamer.Username} Yayında",
                $"Otomatik kayıt başlatıldı. Çıktı dizini: {sessionFolder}",
                "Info");

            _logger.LogInformation("Started segmented FFmpeg recording for streamer {Username} in folder {SessionFolder}", streamer.Username, sessionFolder);
            return log;
        }
        catch
        {
            _sessionStates[streamer.Id] = RecordingSessionState.Failed;
            throw;
        }
        finally
        {
            streamerLock.Release();
        }
    }

    public async Task StopRecordingAsync(int recordingLogId, CancellationToken cancellationToken = default)
    {
        if (!_activeRecordings.TryGetValue(recordingLogId, out var context))
        {
            _logger.LogWarning("Stop recording requested for ID {LogId}, but session was not found.", recordingLogId);
            return;
        }

        var streamerLock = _streamerLocks.GetOrAdd(context.StreamerId, _ => new SemaphoreSlim(1, 1));
        await streamerLock.WaitAsync(cancellationToken);

        try
        {
            _sessionStates[context.StreamerId] = RecordingSessionState.Stopping;
            _logger.LogInformation("Stopping FFmpeg recording session for streamer {Username}", context.StreamerUsername);

            if (!context.Process.HasExited)
            {
                try
                {
                    await context.Process.StandardInput.WriteLineAsync("q");
                    await context.Process.StandardInput.FlushAsync();

                    var exited = await Task.Run(() => context.Process.WaitForExit(5000), cancellationToken);
                    if (!exited && !context.Process.HasExited)
                    {
                        _logger.LogWarning("FFmpeg process unresponsive to 'q' quit. Force killing process tree...");
                        context.Process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception stopping FFmpeg process for LogId {LogId}", recordingLogId);
                    if (!context.Process.HasExited)
                    {
                        context.Process.Kill(entireProcessTree: true);
                    }
                }
            }

            await HandleProcessExitedAsync(recordingLogId, context, context.Process.HasExited ? context.Process.ExitCode : -1);
        }
        finally
        {
            _sessionStates[context.StreamerId] = RecordingSessionState.Idle;
            _activeRecordings.TryRemove(recordingLogId, out _);
            streamerLock.Release();
        }
    }

    private void ParseTelemetryLine(string line, ActiveRecordingContext context)
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
                    context.Telemetry.Duration = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(mins) + TimeSpan.FromSeconds(secs);
                }
            }

            var sizeMatch = Regex.Match(line, @"size=\s*(\d+)kB");
            if (sizeMatch.Success && long.TryParse(sizeMatch.Groups[1].Value, out var sizeKb))
            {
                context.Telemetry.FileSizeBytes = sizeKb * 1024;
            }

            var bitrateMatch = Regex.Match(line, @"bitrate=\s*([\d\.]+)kbits/s");
            if (bitrateMatch.Success && double.TryParse(bitrateMatch.Groups[1].Value, out var bitrateKbps))
            {
                context.Telemetry.BitrateMbps = bitrateKbps / 1000.0;
            }

            if (Directory.Exists(context.SessionFolder))
            {
                var files = Directory.GetFiles(context.SessionFolder, "*.mp4");
                context.Telemetry.SegmentCount = Math.Max(1, files.Length);
            }

            // Phase 5 Periodic Active Disk Check
            var minDiskGB = _settingsService.GetSettings().MinDiskSpaceGB;
            var minDiskBytes = (minDiskGB > 0 ? minDiskGB : 5) * 1024L * 1024L * 1024L;

            if (!_diskService.HasSufficientSpace(context.SessionFolder, minDiskBytes))
            {
                _logger.LogCritical("CRITICAL: Active recording disk space fell below threshold ({Threshold} GB) for streamer {Username}. Requesting graceful shutdown...", minDiskGB, context.StreamerUsername);
                _notificationService.ShowNotification(
                    $"⚠️ Disk Alanı Kritik: {context.StreamerUsername}",
                    $"Disk alanı {minDiskGB} GB limitinin altına düştü. Kayıt otomatik durduruluyor.",
                    "Warning");

                Task.Run(async () => await StopRecordingAsync(context.RecordingLogId));
                return;
            }

            context.Telemetry.LastUpdatedAt = DateTime.Now;
            OnTelemetryUpdated?.Invoke(context.Telemetry);
        }
        catch
        {
            // Ignore telemetry parsing errors
        }
    }

    private async Task HandleProcessExitedAsync(int logId, ActiveRecordingContext context, int exitCode)
    {
        if (_sessionStates.TryGetValue(context.StreamerId, out var currentState))
        {
            // If process exited unexpectedly while recording, trigger connection recovery instead of immediate termination
            if ((exitCode != 0 && exitCode != 255) &&
                currentState == RecordingSessionState.Recording &&
                !context.CancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogWarning("FFmpeg process exited unexpectedly (ExitCode: {ExitCode}) for streamer {Username}. Triggering Resilient Connection Recovery...", exitCode, context.StreamerUsername);
                await AttemptReconnectionAsync(logId, context, exitCode);
                return;
            }
        }

        await FinalizeLogAsync(logId, context, exitCode, (exitCode == 0 || exitCode == 255) ? RecordingStatus.Completed : RecordingStatus.Interrupted);
    }

    private async Task AttemptReconnectionAsync(int logId, ActiveRecordingContext context, int initialExitCode)
    {
        var reconnectLock = _reconnectLocks.GetOrAdd(context.StreamerId, _ => new SemaphoreSlim(1, 1));
        if (!await reconnectLock.WaitAsync(0))
        {
            _logger.LogWarning("Reconnection loop already active for streamer {Username}.", context.StreamerUsername);
            return;
        }

        try
        {
            _sessionStates[context.StreamerId] = RecordingSessionState.Recovering;
            var maxAttempts = _settingsService.GetSettings().MaxReconnectAttempts;
            if (maxAttempts <= 0) maxAttempts = 5;

            var delays = new[] { 5, 10, 20, 30, 60 };
            var token = context.CancellationTokenSource.Token;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (token.IsCancellationRequested || _sessionStates[context.StreamerId] == RecordingSessionState.Stopping)
                {
                    _logger.LogInformation("Reconnection loop canceled for streamer {Username}.", context.StreamerUsername);
                    break;
                }

                var delaySeconds = delays[Math.Min(attempt - 1, delays.Length - 1)];

                context.ReconnectAttempts = attempt;
                context.Telemetry.ReconnectCount = attempt;
                context.Telemetry.IsRecovering = true;
                context.Telemetry.LastError = $"Connection Lost (FFmpeg Exit Code: {initialExitCode})";
                context.Telemetry.FFmpegStatus = $"Reconnecting ({attempt}/{maxAttempts})";
                context.Telemetry.NextRetrySeconds = delaySeconds;
                context.Telemetry.LastUpdatedAt = DateTime.Now;
                OnTelemetryUpdated?.Invoke(context.Telemetry);

                _logger.LogWarning(
                    "Reconnection attempt {Attempt}/{MaxAttempts} initiated for streamer {Username}. Next retry in {Delay}s. Reason: FFmpeg process exited unexpectedly (ExitCode: {ExitCode}).",
                    attempt, maxAttempts, context.StreamerUsername, delaySeconds, initialExitCode);

                for (int s = delaySeconds; s > 0; s--)
                {
                    if (token.IsCancellationRequested || _sessionStates[context.StreamerId] == RecordingSessionState.Stopping)
                        break;

                    context.Telemetry.NextRetrySeconds = s;
                    OnTelemetryUpdated?.Invoke(context.Telemetry);

                    try
                    {
                        await Task.Delay(1000, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                if (token.IsCancellationRequested || _sessionStates[context.StreamerId] == RecordingSessionState.Stopping)
                    break;

                try
                {
                    _logger.LogInformation("Attempting to restart FFmpeg process for streamer {Username} (Attempt {Attempt}/{MaxAttempts})...", context.StreamerUsername, attempt, maxAttempts);

                    var ffmpegPath = _ffmpegLocator.FindFFmpegExecutable();
                    if (string.IsNullOrEmpty(ffmpegPath))
                    {
                        throw new FileNotFoundException("FFmpeg executable not found.");
                    }

                    var newProcess = new Process
                    {
                        StartInfo = context.Process.StartInfo,
                        EnableRaisingEvents = true
                    };

                    newProcess.ErrorDataReceived += (sender, args) =>
                    {
                        if (!string.IsNullOrWhiteSpace(args.Data))
                        {
                            ParseTelemetryLine(args.Data, context);
                        }
                    };

                    newProcess.Exited += (sender, args) =>
                    {
                        Task.Run(async () =>
                        {
                            _logger.LogInformation("FFmpeg process exited for streamer: {Username} (ExitCode: {ExitCode})", context.StreamerUsername, newProcess.ExitCode);
                            await HandleProcessExitedAsync(logId, context, newProcess.ExitCode);
                        });
                    };

                    newProcess.Start();
                    newProcess.BeginErrorReadLine();

                    context.Process = newProcess;
                    _sessionStates[context.StreamerId] = RecordingSessionState.Recording;

                    context.Telemetry.IsRecovering = false;
                    context.Telemetry.FFmpegStatus = "Running";
                    context.Telemetry.LastError = null;
                    context.Telemetry.NextRetrySeconds = 0;
                    context.Telemetry.LastUpdatedAt = DateTime.Now;
                    OnTelemetryUpdated?.Invoke(context.Telemetry);

                    _logger.LogInformation("Reconnection successful for streamer {Username} on attempt {Attempt}. Recording resumed.", context.StreamerUsername, attempt);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reconnection attempt {Attempt} failed for streamer {Username}.", attempt, context.StreamerUsername);
                    context.Telemetry.LastError = ex.Message;
                }
            }

            _logger.LogError("Reconnection failed for streamer {Username} after max attempts. Session marked Interrupted.", context.StreamerUsername);

            context.Telemetry.IsActive = false;
            context.Telemetry.IsRecovering = false;
            context.Telemetry.FFmpegStatus = "Failed";
            context.Telemetry.NextRetrySeconds = 0;
            OnTelemetryUpdated?.Invoke(context.Telemetry);

            await FinalizeLogAsync(logId, context, initialExitCode, RecordingStatus.Interrupted, $"Reconnection failed after {maxAttempts} attempts.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in reconnection loop for streamer {Username}.", context.StreamerUsername);
        }
        finally
        {
            reconnectLock.Release();
        }
    }

    private async Task FinalizeLogAsync(int logId, ActiveRecordingContext context, int exitCode, RecordingStatus defaultStatus, string? customErrorMessage = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRecordingLogRepository>();
            
            var log = await repo.GetByIdAsync(logId);
            if (log == null || log.Status == RecordingStatus.Completed || log.Status == RecordingStatus.Failed)
                return;

            log.EndedAt = DateTime.Now;
            UpdateManifest(context);

            var files = Directory.Exists(context.SessionFolder) 
                ? Directory.GetFiles(context.SessionFolder, "*.mp4")
                : Array.Empty<string>();

            if (files.Length > 0)
            {
                var totalSize = files.Sum(f => new FileInfo(f).Length);
                log.FileSizeBytes = totalSize;
                log.FilePath = files.OrderByDescending(f => f).First();

                if (totalSize > 0 && File.Exists(log.FilePath))
                {
                    try
                    {
                        var validator = scope.ServiceProvider.GetRequiredService<IFFprobeValidator>();
                        var validation = await validator.ValidateAndExtractMetadataAsync(log.FilePath);

                        log.VideoCodec = validation.VideoCodec;
                        log.AudioCodec = validation.AudioCodec;
                        log.Resolution = (validation.Width > 0 && validation.Height > 0) ? $"{validation.Width}x{validation.Height}" : null;
                        log.Duration = validation.Duration;
                        log.IsValidated = validation.IsValid;

                        if (validation.IsValid && defaultStatus == RecordingStatus.Completed)
                        {
                            log.Status = RecordingStatus.Completed;
                            _logger.LogInformation("Recording completed & validated for {Username}. Size: {SizeGB:N2} GB, Res: {Resolution}, Duration: {Duration}", 
                                log.StreamerUsername, totalSize / 1e9, log.Resolution, log.Duration);

                            _notificationService.ShowNotification(
                                $"⏹️ {log.StreamerUsername} Yayını Sona Erdi",
                                $"Kayıt doğrulandı. Toplam boyut: {totalSize / 1e9:N2} GB ({files.Length} parça).",
                                "Info");
                        }
                        else if (!validation.IsValid)
                        {
                            log.Status = RecordingStatus.Corrupt;
                            log.ErrorMessage = $"Validation Failed: {validation.ErrorMessage ?? "Corrupt media container."}";
                            _logger.LogWarning("Recording validation FAILED for {Username}: {Error}", log.StreamerUsername, log.ErrorMessage);
                        }
                        else
                        {
                            log.Status = RecordingStatus.Interrupted;
                            log.ErrorMessage = customErrorMessage ?? $"FFmpeg exited with code {exitCode}. Segments preserved safely.";
                        }
                    }
                    catch (Exception valEx)
                    {
                        _logger.LogWarning(valEx, "FFprobe validation error during log finalization.");
                        log.Status = defaultStatus;
                    }
                }
                else
                {
                    log.Status = RecordingStatus.Corrupt;
                    log.ErrorMessage = "Recording file is empty or missing.";
                }
            }
            else
            {
                log.Status = RecordingStatus.Failed;
                log.ErrorMessage = customErrorMessage ?? "No segment files were created on disk.";
            }

            context.Telemetry.IsActive = false;
            context.Telemetry.FFmpegStatus = "Stopped";
            OnTelemetryUpdated?.Invoke(context.Telemetry);

            await repo.UpdateAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing recording log for LogId {LogId}", logId);
        }
        finally
        {
            _sessionStates[context.StreamerId] = RecordingSessionState.Completed;
        }
    }

    private void UpdateManifest(ActiveRecordingContext context)
    {
        try
        {
            if (!Directory.Exists(context.SessionFolder)) return;

            var manifestPath = Path.Combine(context.SessionFolder, "manifest.json");
            var files = Directory.GetFiles(context.SessionFolder, "*.mp4");

            var manifest = new SegmentManifest
            {
                RecordingLogId = context.RecordingLogId,
                StreamerUsername = context.StreamerUsername,
                SessionStartedAt = context.StartedAt,
                SessionEndedAt = DateTime.Now,
                Segments = files.Select(f => new SegmentItem
                {
                    FileName = Path.GetFileName(f),
                    FilePath = f,
                    CreatedAt = File.GetCreationTime(f),
                    FileSizeBytes = new FileInfo(f).Length
                }).ToList()
            };

            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(manifestPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not update segment manifest.json in {Folder}", context.SessionFolder);
        }
    }

    public List<string> BuildFFmpegArguments(string streamUrl, string outputSegmentPattern, string quality = "En Yüksek (Kaynak)")
    {
        var args = new List<string>
        {
            "-y",
            "-headers",
            "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36\r\nReferer: https://kick.com/\r\nOrigin: https://kick.com\r\n",
            "-fflags",
            "+genpts+discardcorrupt",
            "-avoid_negative_ts",
            "make_zero",
            "-i",
            streamUrl
        };

        if (!string.IsNullOrWhiteSpace(quality))
        {
            var q = quality.ToLowerInvariant();
            if (q.Contains("720"))
            {
                args.Add("-map"); args.Add("0:v:1?");
                args.Add("-map"); args.Add("0:a:0?");
            }
            else if (q.Contains("480"))
            {
                args.Add("-map"); args.Add("0:v:2?");
                args.Add("-map"); args.Add("0:a:0?");
            }
            else if (q.Contains("360"))
            {
                args.Add("-map"); args.Add("0:v:3?");
                args.Add("-map"); args.Add("0:a:0?");
            }
        }

        args.AddRange(new[]
        {
            "-c", "copy",
            "-bsf:a", "aac_adtstoasc",
            "-movflags", "+faststart+frag_keyframe+empty_moov",
            "-f", "segment",
            "-segment_time", "1800",
            "-segment_format", "mp4",
            "-reset_timestamps", "1",
            "-break_non_keyframes", "1",
            "-segment_start_number", "1",
            outputSegmentPattern
        });

        return args;
    }

    public void Dispose()
    {
        foreach (var context in _activeRecordings.Values)
        {
            try
            {
                if (!context.Process.HasExited)
                {
                    context.Process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore process termination errors on shutdown
            }
        }
        _activeRecordings.Clear();
        _sessionStates.Clear();
    }
}
