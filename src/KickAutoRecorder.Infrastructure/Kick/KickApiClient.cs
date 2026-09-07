using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Infrastructure.Kick;

public class KickApiClient : IKickApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KickApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public KickApiClient(HttpClient httpClient, ILogger<KickApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        ConfigureDefaultHeaders();
    }

    private void ConfigureDefaultHeaders()
    {
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
        }
        if (!_httpClient.DefaultRequestHeaders.Contains("Accept"))
        {
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        }
        if (!_httpClient.DefaultRequestHeaders.Contains("Accept-Language"))
        {
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        }
    }

    public async Task<KickChannelStatusResult> CheckChannelStatusAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return new KickChannelStatusResult(
                Username: username ?? string.Empty,
                Status: StreamStatus.Error,
                StreamTitle: string.Empty,
                PlaybackUrl: string.Empty,
                ViewerCount: 0,
                Category: string.Empty,
                ErrorMessage: "Streamer username is empty."
            );
        }

        var endpoint = $"https://kick.com/api/v2/channels/{username.ToLowerInvariant()}";
        _logger.LogInformation("Checking Kick livestream status for: {Username} via endpoint {Endpoint}", username, endpoint);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Referrer = new Uri("https://kick.com/");

            using var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Kick API HTTP {StatusCode} for channel {Username}", response.StatusCode, username);
                return new KickChannelStatusResult(
                    Username: username,
                    Status: StreamStatus.Error,
                    StreamTitle: string.Empty,
                    PlaybackUrl: string.Empty,
                    ViewerCount: 0,
                    Category: string.Empty,
                    ErrorMessage: $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})"
                );
            }

            var channelData = await response.Content.ReadFromJsonAsync<KickChannelResponse>(JsonOptions, cts.Token);

            if (channelData == null)
            {
                return new KickChannelStatusResult(
                    Username: username,
                    Status: StreamStatus.Error,
                    StreamTitle: string.Empty,
                    PlaybackUrl: string.Empty,
                    ViewerCount: 0,
                    Category: string.Empty,
                    ErrorMessage: "Empty or invalid JSON payload received from Kick API."
                );
            }

            // Kick API can return a non-null livestream object even when offline (with last stream data).
            // Must explicitly check the is_live field to determine actual live status.
            var isLive = channelData.Livestream != null && channelData.Livestream.IsLive;
            var status = isLive ? StreamStatus.Live : StreamStatus.Offline;

            var title = channelData.Livestream?.SessionTitle 
                        ?? channelData.Livestream?.Title 
                        ?? string.Empty;

            var viewers = channelData.Livestream?.ViewerCount 
                         ?? channelData.Livestream?.Viewers 
                         ?? 0;

            var category = channelData.Livestream?.Category?.Name 
                          ?? (channelData.Livestream?.Categories != null && channelData.Livestream.Categories.Length > 0 ? channelData.Livestream.Categories[0].Name : "Uncategorized");

            var playbackUrl = channelData.Livestream?.Source 
                             ?? channelData.PlaybackUrl 
                             ?? string.Empty;

            _logger.LogInformation("Kick Channel {Username} status: {Status} (Title: '{Title}', Viewers: {Viewers})", 
                username, status, title, viewers);

            return new KickChannelStatusResult(
                Username: username,
                Status: status,
                StreamTitle: title,
                PlaybackUrl: playbackUrl,
                ViewerCount: viewers,
                Category: category,
                ErrorMessage: null
            );
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Kick API request timed out after 10 seconds for {Username}", username);
            return new KickChannelStatusResult(
                Username: username,
                Status: StreamStatus.Error,
                StreamTitle: string.Empty,
                PlaybackUrl: string.Empty,
                ViewerCount: 0,
                Category: string.Empty,
                ErrorMessage: "Request timed out (10s)."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered while querying Kick API for {Username}", username);
            return new KickChannelStatusResult(
                Username: username,
                Status: StreamStatus.Error,
                StreamTitle: string.Empty,
                PlaybackUrl: string.Empty,
                ViewerCount: 0,
                Category: string.Empty,
                ErrorMessage: ex.Message
            );
        }
    }
}
