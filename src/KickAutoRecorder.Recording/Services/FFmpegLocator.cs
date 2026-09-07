using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Recording.Services;

public class FFmpegLocator : IFFmpegLocator
{
    private readonly ISettingsService _settingsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FFmpegLocator> _logger;

    public FFmpegLocator(ISettingsService settingsService, IConfiguration configuration, ILogger<FFmpegLocator> logger)
    {
        _settingsService = settingsService;
        _configuration = configuration;
        _logger = logger;
    }

    public string? FindFFmpegExecutable()
    {
        // 1. Configured Path from Settings Service
        var configuredPath = _settingsService.GetSettings().FFmpegPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = _configuration["RecordingSettings:FFmpegPath"];
        }
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            _logger.LogInformation("FFmpeg found via appsettings configuration: {Path}", configuredPath);
            return configuredPath;
        }

        // 2. Relative App Base Directory
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var relativeCandidates = new[]
        {
            Path.Combine(baseDir, "ffmpeg.exe"),
            Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe"),
            Path.Combine(baseDir, "ffmpeg", "bin", "ffmpeg.exe"),
            Path.Combine(baseDir, "runtimes", "ffmpeg.exe")
        };

        foreach (var candidate in relativeCandidates)
        {
            if (File.Exists(candidate))
            {
                _logger.LogInformation("FFmpeg found in application folder: {Path}", candidate);
                return candidate;
            }
        }

        // 3. System PATH Environment Variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            var paths = pathEnv.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                try
                {
                    var fullPath = Path.Combine(path.Trim(), "ffmpeg.exe");
                    if (File.Exists(fullPath))
                    {
                        _logger.LogInformation("FFmpeg found in System PATH: {Path}", fullPath);
                        return fullPath;
                    }
                }
                catch
                {
                    // Ignore invalid path characters in PATH env
                }
            }
        }

        _logger.LogWarning("FFmpeg executable (ffmpeg.exe) could not be located in Config, Local Folder, or System PATH.");
        return null;
    }

    public bool IsFFmpegAvailable()
    {
        return FindFFmpegExecutable() != null;
    }
}
