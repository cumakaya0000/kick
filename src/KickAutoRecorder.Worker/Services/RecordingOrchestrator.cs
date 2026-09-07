using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Worker.Services;

public class RecordingOrchestrator : IRecordingOrchestrator
{
    private readonly IStreamerService _streamerService;
    private readonly IRecordingEngine _recordingEngine;
    private readonly IRecordingLogRepository _recordingLogRepository;
    private readonly ISettingsService _settingsService;
    private readonly IFFprobeValidator _ffprobeValidator;
    private readonly ILogger<RecordingOrchestrator> _logger;

    // Debounce counters: Live requires 2 consecutive checks, Offline requires 3
    private readonly ConcurrentDictionary<int, int> _liveDebounceCounters = new();
    private readonly ConcurrentDictionary<int, int> _offlineDebounceCounters = new();
    private readonly ConcurrentDictionary<int, byte> _manualStoppedStreamers = new();
    private readonly ConcurrentDictionary<int, byte> _diskSuppressedStreamers = new();

    private const int RequiredLiveConfirmations = 2;
    private const int RequiredOfflineConfirmations = 3;

    public RecordingOrchestrator(
        IStreamerService streamerService,
        IRecordingEngine recordingEngine,
        IRecordingLogRepository recordingLogRepository,
        ISettingsService settingsService,
        IFFprobeValidator ffprobeValidator,
        ILogger<RecordingOrchestrator> logger)
    {
        _streamerService = streamerService;
        _recordingEngine = recordingEngine;
        _recordingLogRepository = recordingLogRepository;
        _settingsService = settingsService;
        _ffprobeValidator = ffprobeValidator;
        _logger = logger;
    }

    public void NotifyManualStop(int streamerId)
    {
        _manualStoppedStreamers[streamerId] = 1;
        _logger.LogInformation("Manual stop registered for streamer ID {StreamerId}. Auto-restart suppressed while stream remains live.", streamerId);
    }

    public async Task ProcessStreamerCheckAsync(Streamer streamer, CancellationToken cancellationToken = default)
    {
        if (streamer == null || !streamer.IsTrackingEnabled)
            return;

        _logger.LogDebug("Orchestrator processing streamer status check for: {Username}", streamer.Username);

        var updatedStreamer = await _streamerService.CheckAndUpdateStreamerStatusAsync(streamer.Id, cancellationToken);
        if (updatedStreamer == null)
            return;

        // Issue F Fix: API errors (StreamStatus.Error) must NOT alter live/offline debounce counters or trigger premature stop
        if (updatedStreamer.CurrentStatus == StreamStatus.Error)
        {
            _logger.LogWarning("API status check for {Username} returned Error status. Preserving current recording state and skipping debounce updates.", streamer.Username);
            return;
        }

        var isRecording = _recordingEngine.IsRecording(updatedStreamer.Id);
        var isLive = updatedStreamer.CurrentStatus == StreamStatus.Live;

        var settings = _settingsService.GetSettings();
        var outputDirectory = string.IsNullOrWhiteSpace(settings.OutputFolder) ? "Recordings" : settings.OutputFolder;

        // Debounce Logic for Live Confirmation
        if (isLive)
        {
            _offlineDebounceCounters[streamer.Id] = 0; // Reset offline count
            var currentLiveCount = _liveDebounceCounters.AddOrUpdate(streamer.Id, 1, (_, count) => count + 1);

            _logger.LogDebug("Debounce status for {Username}: LIVE count = {LiveCount}/{Required}", 
                streamer.Username, currentLiveCount, RequiredLiveConfirmations);

            if (currentLiveCount >= RequiredLiveConfirmations && !isRecording)
            {
                // Issue C Fix: Check if user manually stopped recording
                if (_manualStoppedStreamers.ContainsKey(streamer.Id))
                {
                    _logger.LogDebug("Auto-start skipped for {Username}: User manually stopped recording during this live session.", streamer.Username);
                    return;
                }

                if (string.IsNullOrWhiteSpace(updatedStreamer.PlaybackUrl))
                {
                    _logger.LogWarning("Streamer {Username} confirmed LIVE but HLS playback URL is missing.", updatedStreamer.Username);
                    return;
                }

                _logger.LogInformation("DEBOUNCE CONFIRMED LIVE: Starting recording for {Username} ({PlaybackUrl})", 
                    updatedStreamer.Username, updatedStreamer.PlaybackUrl);

                try
                {
                    await _recordingEngine.StartRecordingAsync(updatedStreamer, updatedStreamer.PlaybackUrl, outputDirectory, cancellationToken);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Low disk space"))
                {
                    // Issue I Fix: Low disk space suppression
                    _diskSuppressedStreamers[streamer.Id] = 1;
                    _logger.LogWarning("Recording start suppressed for {Username} due to insufficient disk space.", streamer.Username);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start recording for streamer {Username}", updatedStreamer.Username);
                }
            }
        }
        else
        {
            // Streamer is Offline - clear manual stop & disk suppression flags for next stream session
            _manualStoppedStreamers.TryRemove(streamer.Id, out _);
            _diskSuppressedStreamers.TryRemove(streamer.Id, out _);
            _liveDebounceCounters[streamer.Id] = 0; // Reset live count
            var currentOfflineCount = _offlineDebounceCounters.AddOrUpdate(streamer.Id, 1, (_, count) => count + 1);

            _logger.LogDebug("Debounce status for {Username}: OFFLINE count = {OfflineCount}/{Required}", 
                streamer.Username, currentOfflineCount, RequiredOfflineConfirmations);

            if (currentOfflineCount >= RequiredOfflineConfirmations && isRecording)
            {
                _logger.LogInformation("DEBOUNCE CONFIRMED OFFLINE: Stopping recording session for {Username}", updatedStreamer.Username);

                var activeLogs = await _recordingLogRepository.GetActiveRecordingsAsync(cancellationToken);
                var activeLog = activeLogs.FirstOrDefault(r => r.StreamerId == updatedStreamer.Id);

                if (activeLog != null)
                {
                    try
                    {
                        await _recordingEngine.StopRecordingAsync(activeLog.Id, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error stopping recording session for streamer {Username}", updatedStreamer.Username);
                    }
                }
            }
        }
    }

    public async Task RecoverUnfinishedRecordingsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing Production-Grade Startup Crash Recovery & Orphan FFmpeg Detection...");

        try
        {
            // Step 1 & 2: Idempotent Query - Fetches ONLY logs stuck in Recording or Pending status
            var activeLogs = await _recordingLogRepository.GetActiveRecordingsAsync(cancellationToken);
            var orphanedLogs = activeLogs.ToList();

            if (!orphanedLogs.Any())
            {
                _logger.LogInformation("No unfinished or orphaned recording sessions found in database. Recovery check complete (0 pending).");
                return;
            }

            _logger.LogWarning("Found {Count} unfinished recording sessions stuck in in-flight status. Commencing recovery scan...", orphanedLogs.Count);

            foreach (var log in orphanedLogs)
            {
                _logger.LogInformation("Processing crash recovery for LogId {LogId} (Streamer: '{Username}', File: '{FilePath}')...",
                    log.Id, log.StreamerUsername, log.FilePath);

                // Step 3: Targeted Orphan FFmpeg Process Cleanup via ProcessId
                if (log.ProcessId.HasValue)
                {
                    try
                    {
                        var proc = Process.GetProcessById(log.ProcessId.Value);
                        if (proc != null && !proc.HasExited && proc.ProcessName.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("Terminating orphaned FFmpeg process (PID: {Pid}) for streamer {Username}...", log.ProcessId.Value, log.StreamerUsername);
                            proc.Kill(entireProcessTree: true);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Process is not running, which is fine
                    }
                    catch (Exception procEx)
                    {
                        _logger.LogDebug(procEx, "Ignored process termination error for PID {Pid}", log.ProcessId.Value);
                    }
                }

                log.EndedAt = DateTime.Now;

                // Step 4: File Existence Check
                if (!string.IsNullOrWhiteSpace(log.FilePath) && File.Exists(log.FilePath))
                {
                    var fileInfo = new FileInfo(log.FilePath);
                    log.FileSizeBytes = fileInfo.Length;
                    _logger.LogInformation("File found on disk for LogId {LogId}. Size: {FileSizeBytes:N0} bytes ({SizeMB:N2} MB).", log.Id, fileInfo.Length, fileInfo.Length / 1e6);

                    // Step 5: FFprobe Metadata & Container Validation
                    if (fileInfo.Length > 0)
                    {
                        var validation = await _ffprobeValidator.ValidateAndExtractMetadataAsync(log.FilePath, cancellationToken);
                        log.VideoCodec = validation.VideoCodec;
                        log.AudioCodec = validation.AudioCodec;
                        log.Resolution = (validation.Width > 0 && validation.Height > 0) ? $"{validation.Width}x{validation.Height}" : null;
                        log.Duration = validation.Duration;
                        log.IsValidated = validation.IsValid;

                        // Step 6: Status Classification based on validation & process state
                        if (validation.IsValid)
                        {
                            log.Status = RecordingStatus.Recovered;
                            log.ErrorMessage = $"Recovered on startup. Duration: {validation.Duration:hh\\:mm\\:ss}, Res: {log.Resolution}, Codecs: {validation.VideoCodec}/{validation.AudioCodec}";
                            _logger.LogInformation("Recovery SUCCESS for LogId {LogId}: Status=Recovered, Duration={Duration}, Res={Resolution}", log.Id, log.Duration, log.Resolution);
                        }
                        else
                        {
                            log.Status = RecordingStatus.Interrupted;
                            log.ErrorMessage = $"App restarted during active recording. Partial video saved ({fileInfo.Length / 1e6:N1} MB).";
                            _logger.LogWarning("Recovery PARTIAL for LogId {LogId}: Status=Interrupted ({SizeMB:N1} MB saved). Validation: {Error}", log.Id, fileInfo.Length / 1e6, validation.ErrorMessage);
                        }
                    }
                    else
                    {
                        log.Status = RecordingStatus.Corrupt;
                        log.IsValidated = false;
                        log.ErrorMessage = "Application restarted during recording. Zero-byte file corrupt.";
                        _logger.LogWarning("Recovery FAILED for LogId {LogId}: Status=Corrupt (Zero-byte file).", log.Id);
                    }
                }
                else
                {
                    log.Status = RecordingStatus.Interrupted;
                    log.IsValidated = false;
                    log.ErrorMessage = "Application restarted during active recording. File missing from disk.";
                    _logger.LogWarning("Recovery FAILED for LogId {LogId}: Status=Interrupted (File missing).", log.Id);
                }

                // Step 7: Idempotent DB Update (Status changed away from Recording/Pending)
                await _recordingLogRepository.UpdateAsync(log, cancellationToken);
                _logger.LogInformation("Successfully finalized recovered log {LogId} for {Username}. Final Status: {Status}", 
                    log.Id, log.StreamerUsername, log.Status);
            }

            _logger.LogInformation("Startup Crash Recovery completed for {Count} logs.", orphanedLogs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during advanced startup crash & orphan recovery scan.");
        }
    }

    public async Task StopAllActiveRecordingsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing graceful shutdown for all active recording sessions...");

        try
        {
            var activeLogs = await _recordingLogRepository.GetActiveRecordingsAsync(cancellationToken);
            foreach (var log in activeLogs)
            {
                try
                {
                    await _recordingEngine.StopRecordingAsync(log.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping active recording log {LogId} during shutdown.", log.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping active recordings during application shutdown.");
        }
    }
}
