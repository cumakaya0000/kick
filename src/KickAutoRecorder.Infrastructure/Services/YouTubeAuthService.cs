using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;

using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Infrastructure.Services;

public class YouTubeAuthService : IYouTubeAuthService
{
    private static readonly string[] Scopes = new[]
    {
        YouTubeService.Scope.YoutubeUpload,
        YouTubeService.Scope.YoutubeReadonly
    };

    private readonly IConfiguration _configuration;
    private readonly IYouTubeCredentialStore _credentialStore;
    private readonly ILogger<YouTubeAuthService> _logger;

    public YouTubeAuthService(
        IConfiguration configuration,
        IYouTubeCredentialStore credentialStore,
        ILogger<YouTubeAuthService> logger)
    {
        _configuration = configuration;
        _credentialStore = credentialStore;
        _logger = logger;
    }

    private string GetSecretsFilePath()
    {
        var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KickVideo");
        if (!Directory.Exists(appDataFolder))
        {
            Directory.CreateDirectory(appDataFolder);
        }
        return Path.Combine(appDataFolder, "youtube_client_secrets.json");
    }

    public bool IsOAuthConfigured()
    {
        var secrets = GetClientSecrets();
        return !string.IsNullOrWhiteSpace(secrets.ClientId) && !string.IsNullOrWhiteSpace(secrets.ClientSecret);
    }

    public async Task<bool> ImportClientSecretsJsonAsync(string sourceJsonFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceJsonFilePath) || !File.Exists(sourceJsonFilePath))
        {
            _logger.LogWarning("Import OAuth JSON failed: File does not exist.");
            return false;
        }

        try
        {
            using (var stream = File.OpenRead(sourceJsonFilePath))
            {
                var loadedSecrets = await GoogleClientSecrets.FromStreamAsync(stream, cancellationToken);
                if (loadedSecrets?.Secrets == null || string.IsNullOrWhiteSpace(loadedSecrets.Secrets.ClientId))
                {
                    _logger.LogWarning("Import OAuth JSON failed: Valid Desktop OAuth Client ID could not be found in file.");
                    return false;
                }
            }

            var destPath = GetSecretsFilePath();
            File.Copy(sourceJsonFilePath, destPath, overwrite: true);
            _logger.LogInformation("OAuth Client Secrets JSON successfully imported to AppData.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import OAuth Client Secrets JSON file.");
            return false;
        }
    }

    private ClientSecrets GetClientSecrets()
    {
        var secretsFilePath = GetSecretsFilePath();
        if (File.Exists(secretsFilePath))
        {
            try
            {
                using var stream = File.OpenRead(secretsFilePath);
                var loaded = GoogleClientSecrets.FromStream(stream);
                if (loaded?.Secrets != null && !string.IsNullOrWhiteSpace(loaded.Secrets.ClientId))
                {
                    return loaded.Secrets;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse imported client secrets JSON file.");
            }
        }

        var clientId = _configuration["YouTube:ClientId"];
        var clientSecret = _configuration["YouTube:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            clientId = _configuration["YouTubeOAuth:ClientId"] ?? "";
            clientSecret = _configuration["YouTubeOAuth:ClientSecret"] ?? "";
        }

        return new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        };
    }

    public async Task<UserCredential?> GetUserCredentialAsync(CancellationToken cancellationToken = default)
    {
        var secrets = GetClientSecrets();
        if (string.IsNullOrWhiteSpace(secrets.ClientId) || string.IsNullOrWhiteSpace(secrets.ClientSecret))
        {
            _logger.LogWarning("YouTube OAuth ClientId or ClientSecret is missing in application configuration.");
            return null;
        }

        try
        {
            var dataStore = new DpapiFileDataStore(_credentialStore);
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = secrets,
                Scopes = Scopes,
                DataStore = dataStore
            });

            var token = await dataStore.GetAsync<TokenResponse>("user");
            if (token == null)
            {
                return null;
            }

            var credential = new UserCredential(flow, "user", token);
            if (credential.Token.IsStale)
            {
                var refreshed = await credential.RefreshTokenAsync(cancellationToken);
                if (!refreshed)
                {
                    _logger.LogWarning("Failed to automatically refresh YouTube OAuth token.");
                    return null;
                }
            }

            return credential;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored YouTube UserCredential.");
            return null;
        }
    }

    public async Task<YouTubeService?> GetYouTubeServiceAsync(CancellationToken cancellationToken = default)
    {
        var credential = await GetUserCredentialAsync(cancellationToken);
        if (credential == null)
        {
            return null;
        }

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "KickVideo"
        });
    }

    public async Task<YouTubeAccountInfo> GetAccountInfoAsync(CancellationToken cancellationToken = default)
    {
        var info = new YouTubeAccountInfo();
        try
        {
            var service = await GetYouTubeServiceAsync(cancellationToken);
            if (service == null)
            {
                info.IsConnected = false;
                info.StatusMessage = "Bağlı Değil (Yetkilendirme Gerekli)";
                return info;
            }

            var request = service.Channels.List("snippet");
            request.Mine = true;
            var response = await request.ExecuteAsync(cancellationToken);

            var channel = response.Items?.FirstOrDefault();
            if (channel != null)
            {
                info.IsConnected = true;
                info.ChannelTitle = channel.Snippet?.Title ?? "Bilinmeyen Kanal";
                info.ChannelId = channel.Id ?? "";
                info.ChannelThumbnailUrl = channel.Snippet?.Thumbnails?.Default__?.Url;
                info.StatusMessage = "Bağlı";
            }
            else
            {
                info.IsConnected = true;
                info.ChannelTitle = "YouTube Hesabı";
                info.StatusMessage = "Bağlı (Kanal Bulunamadı)";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get YouTube account info.");
            info.IsConnected = false;
            info.StatusMessage = "Bağlantı Hatası veya Süresi Doldu";
        }

        return info;
    }

    public async Task<YouTubeAccountInfo> ConnectAccountAsync(CancellationToken cancellationToken = default)
    {
        var secrets = GetClientSecrets();
        if (string.IsNullOrWhiteSpace(secrets.ClientId) || string.IsNullOrWhiteSpace(secrets.ClientSecret))
        {
            throw new InvalidOperationException("Google Cloud OAuth Client ID ve Client Secret konfigürasyonda bulunamadı. Lütfen appsettings.json dosyasında YouTube:ClientId ve YouTube:ClientSecret değerlerini tanımlayın.");
        }

        var dataStore = new DpapiFileDataStore(_credentialStore);
        _logger.LogInformation("Initiating Desktop OAuth authorization flow for YouTube...");

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            Scopes,
            "user",
            cancellationToken,
            dataStore);

        if (credential == null || credential.Token == null)
        {
            throw new InvalidOperationException("YouTube OAuth yetkilendirmesi başarısız oldu.");
        }

        _logger.LogInformation("YouTube OAuth authorization completed successfully.");
        return await GetAccountInfoAsync(cancellationToken);
    }

    public async Task DisconnectAccountAsync(CancellationToken cancellationToken = default)
    {
        await _credentialStore.ClearTokenAsync(cancellationToken);
        _logger.LogInformation("YouTube account disconnected and token removed.");
    }

    public async Task<YouTubeAccountInfo> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await GetAccountInfoAsync(cancellationToken);
    }

    // Helper class adapting IYouTubeCredentialStore to IDataStore for Google SDK
    private class DpapiFileDataStore : IDataStore
    {
        private readonly IYouTubeCredentialStore _credentialStore;

        public DpapiFileDataStore(IYouTubeCredentialStore credentialStore)
        {
            _credentialStore = credentialStore;
        }

        public async Task StoreAsync<T>(string key, T value)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            await _credentialStore.SaveTokenAsync(json);
        }

        public async Task DeleteAsync<T>(string key)
        {
            await _credentialStore.ClearTokenAsync();
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var json = await _credentialStore.LoadTokenAsync();
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                return default;
            }
        }

        public Task ClearAsync()
        {
            return _credentialStore.ClearTokenAsync();
        }
    }
}
