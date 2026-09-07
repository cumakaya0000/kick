using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.YouTube.v3;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Core.Interfaces;

public interface IYouTubeAuthService
{
    bool IsOAuthConfigured();
    Task<bool> ImportClientSecretsJsonAsync(string sourceJsonFilePath, CancellationToken cancellationToken = default);
    Task<YouTubeAccountInfo> GetAccountInfoAsync(CancellationToken cancellationToken = default);
    Task<UserCredential?> GetUserCredentialAsync(CancellationToken cancellationToken = default);
    Task<YouTubeService?> GetYouTubeServiceAsync(CancellationToken cancellationToken = default);
    Task<YouTubeAccountInfo> ConnectAccountAsync(CancellationToken cancellationToken = default);
    Task DisconnectAccountAsync(CancellationToken cancellationToken = default);
    Task<YouTubeAccountInfo> TestConnectionAsync(CancellationToken cancellationToken = default);
}
