using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Infrastructure.Services;

public class WindowsYouTubeCredentialStore : IYouTubeCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("KickVideo.YouTubeToken.Entropy.v1");
    private readonly ISettingsService _settingsService;
    private readonly ILogger<WindowsYouTubeCredentialStore> _logger;

    public WindowsYouTubeCredentialStore(
        ISettingsService settingsService,
        ILogger<WindowsYouTubeCredentialStore> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    private string GetTokenFilePath()
    {
        var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KickVideo");
        if (!Directory.Exists(appDataFolder))
        {
            Directory.CreateDirectory(appDataFolder);
        }
        return Path.Combine(appDataFolder, "yt_token.dat");
    }

    public async Task SaveTokenAsync(string tokenDataJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenDataJson))
        {
            await ClearTokenAsync(cancellationToken);
            return;
        }

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(tokenDataJson);
            var encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            var filePath = GetTokenFilePath();
            await File.WriteAllBytesAsync(filePath, encryptedBytes, cancellationToken);
            _logger.LogInformation("YouTube OAuth token securely encrypted and saved using Windows DPAPI.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to securely save encrypted YouTube OAuth token.");
            throw;
        }
    }

    public async Task<string?> LoadTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = GetTokenFilePath();
            if (!File.Exists(filePath))
            {
                return null;
            }

            var encryptedBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            if (encryptedBytes.Length == 0)
            {
                return null;
            }

            var plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt stored YouTube OAuth token. Token will be reset.");
            return null;
        }
    }

    public Task ClearTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = GetTokenFilePath();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Stored encrypted YouTube OAuth token deleted.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting encrypted YouTube OAuth token file.");
        }
        return Task.CompletedTask;
    }
}
