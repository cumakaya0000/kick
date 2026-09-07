using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Infrastructure.Services;

public class FFprobeValidator : IFFprobeValidator
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<FFprobeValidator> _logger;

    public FFprobeValidator(ISettingsService settingsService, ILogger<FFprobeValidator> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public string? FindFFprobeExecutable()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "ffprobe.exe"),
            Path.Combine(baseDir, "ffmpeg", "ffprobe.exe"),
            Path.Combine(baseDir, "ffmpeg", "bin", "ffprobe.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var path in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    var fullPath = Path.Combine(path.Trim(), "ffprobe.exe");
                    if (File.Exists(fullPath))
                        return fullPath;
                }
                catch { }
            }
        }

        return null;
    }

    public async Task<FFprobeMetadata> ValidateAndExtractMetadataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = new FFprobeMetadata();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            result.IsValid = false;
            result.ErrorMessage = "File does not exist on disk.";
            return result;
        }

        var fileInfo = new FileInfo(filePath);
        result.FileSizeBytes = fileInfo.Length;

        if (fileInfo.Length == 0)
        {
            result.IsValid = false;
            result.ErrorMessage = "File is zero bytes.";
            return result;
        }

        var ffprobeExe = FindFFprobeExecutable();
        if (string.IsNullOrEmpty(ffprobeExe))
        {
            // If ffprobe.exe is missing, fallback to basic non-zero byte validation
            _logger.LogWarning("ffprobe.exe not found on system. Falling back to file length check.");
            result.IsValid = fileInfo.Length > 1024; // > 1 KB
            result.ErrorMessage = result.IsValid ? null : "File is too small (< 1KB).";
            return result;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffprobeExe,
                Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);

            var jsonOutput = await outputTask;
            if (string.IsNullOrWhiteSpace(jsonOutput))
            {
                result.IsValid = false;
                result.ErrorMessage = "FFprobe produced empty output.";
                return result;
            }

            using var doc = JsonDocument.Parse(jsonOutput);
            var root = doc.RootElement;

            if (root.TryGetProperty("format", out var formatProp))
            {
                if (formatProp.TryGetProperty("duration", out var durProp) && double.TryParse(durProp.GetString(), out var durSec))
                {
                    result.Duration = TimeSpan.FromSeconds(durSec);
                }

                if (formatProp.TryGetProperty("bit_rate", out var bitProp) && long.TryParse(bitProp.GetString(), out var bitRate))
                {
                    result.Bitrate = bitRate;
                }
            }

            if (root.TryGetProperty("streams", out var streamsProp) && streamsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var stream in streamsProp.EnumerateArray())
                {
                    if (stream.TryGetProperty("codec_type", out var typeProp))
                    {
                        var codecType = typeProp.GetString();
                        if (codecType == "video")
                        {
                            if (stream.TryGetProperty("codec_name", out var cName))
                                result.VideoCodec = cName.GetString() ?? string.Empty;
                            if (stream.TryGetProperty("width", out var w))
                                result.Width = w.GetInt32();
                            if (stream.TryGetProperty("height", out var h))
                                result.Height = h.GetInt32();
                        }
                        else if (codecType == "audio")
                        {
                            if (stream.TryGetProperty("codec_name", out var aName))
                                result.AudioCodec = aName.GetString() ?? string.Empty;
                        }
                    }
                }
            }

            // Valid if process exit code is 0, video stream exists, audio stream exists, and duration > 0
            var hasVideo = !string.IsNullOrWhiteSpace(result.VideoCodec);
            var hasAudio = !string.IsNullOrWhiteSpace(result.AudioCodec);
            var hasDuration = result.Duration.TotalSeconds > 0;

            result.IsValid = process.ExitCode == 0 && hasVideo && (hasAudio || hasDuration);
            if (!result.IsValid)
            {
                if (process.ExitCode != 0)
                    result.ErrorMessage = $"FFprobe exited with non-zero exit code: {process.ExitCode}";
                else if (!hasVideo)
                    result.ErrorMessage = "Video stream track is missing or invalid.";
                else
                    result.ErrorMessage = "Corrupt container or missing stream tracks.";
            }

            _logger.LogInformation("FFprobe validation completed for {FilePath}: Valid={IsValid}, Duration={Duration}, Resolution={Width}x{Height}, VideoCodec={VideoCodec}, AudioCodec={AudioCodec}",
                filePath, result.IsValid, result.Duration, result.Width, result.Height, result.VideoCodec, result.AudioCodec);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception running FFprobe validation for {FilePath}", filePath);
            result.IsValid = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}
