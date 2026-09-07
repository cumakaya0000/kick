using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsFilePath;
    private RecordingSettings _currentSettings;

    public SettingsService(IConfiguration configuration, ILogger<SettingsService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "usersettings.json");

        _currentSettings = LoadSettings();
    }

    public RecordingSettings GetSettings()
    {
        return new RecordingSettings
        {
            OutputFolder = _currentSettings.OutputFolder,
            FFmpegPath = _currentSettings.FFmpegPath,
            PollingIntervalSeconds = _currentSettings.PollingIntervalSeconds,
            MinDiskSpaceGB = _currentSettings.MinDiskSpaceGB,
            PreferredQuality = _currentSettings.PreferredQuality,
            IsDarkTheme = _currentSettings.IsDarkTheme
        };
    }

    public async Task UpdateSettingsAsync(RecordingSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        _currentSettings = new RecordingSettings
        {
            OutputFolder = string.IsNullOrWhiteSpace(settings.OutputFolder) ? "Recordings" : settings.OutputFolder,
            FFmpegPath = string.IsNullOrWhiteSpace(settings.FFmpegPath) ? null : settings.FFmpegPath,
            PollingIntervalSeconds = settings.PollingIntervalSeconds <= 0 ? 30 : settings.PollingIntervalSeconds,
            MinDiskSpaceGB = settings.MinDiskSpaceGB <= 0 ? 5 : settings.MinDiskSpaceGB,
            PreferredQuality = string.IsNullOrWhiteSpace(settings.PreferredQuality) ? "En Yüksek (Kaynak)" : settings.PreferredQuality,
            IsDarkTheme = settings.IsDarkTheme
        };

        try
        {
            var json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _settingsFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            File.Move(tempPath, _settingsFilePath, overwrite: true);
            _logger.LogInformation("Application settings updated atomically to {SettingsPath}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist user settings to file {SettingsPath}", _settingsFilePath);
        }
    }

    private RecordingSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<RecordingSettings>(json);
                if (loaded != null)
                {
                    _logger.LogInformation("User settings loaded from {SettingsPath}", _settingsFilePath);
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load settings from file {SettingsPath}. Falling back to appsettings.", _settingsFilePath);
        }

        // Fallback to IConfiguration appsettings.json values
        var defaultFolder = @"C:\Users\kcuma\Desktop\anaklasor\projelerim\kick\kayitlar";
        var outputFolder = _configuration["RecordingSettings:OutputFolder"] ?? defaultFolder;
        var ffmpegPath = _configuration["RecordingSettings:FFmpegPath"];
        _ = int.TryParse(_configuration["RecordingSettings:PollingIntervalSeconds"], out var pollingSeconds);
        _ = long.TryParse(_configuration["RecordingSettings:MinDiskSpaceGB"], out var minDiskGB);

        return new RecordingSettings
        {
            OutputFolder = string.IsNullOrWhiteSpace(outputFolder) ? defaultFolder : outputFolder,
            FFmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? null : ffmpegPath,
            PollingIntervalSeconds = pollingSeconds > 0 ? pollingSeconds : 30,
            MinDiskSpaceGB = minDiskGB > 0 ? minDiskGB : 5,
            PreferredQuality = "En Yüksek (Kaynak)",
            IsDarkTheme = true
        };
    }
}
