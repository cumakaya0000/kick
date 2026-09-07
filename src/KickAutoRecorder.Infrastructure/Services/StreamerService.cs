using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Infrastructure.Services;

public class StreamerService : IStreamerService
{
    private readonly IStreamerRepository _streamerRepository;
    private readonly IKickApiClient _kickApiClient;
    private readonly ILogger<StreamerService> _logger;

    public StreamerService(
        IStreamerRepository streamerRepository, 
        IKickApiClient kickApiClient,
        ILogger<StreamerService> logger)
    {
        _streamerRepository = streamerRepository;
        _kickApiClient = kickApiClient;
        _logger = logger;
    }

    public async Task<IEnumerable<Streamer>> GetAllStreamersAsync(CancellationToken cancellationToken = default)
    {
        return await _streamerRepository.GetAllAsync(cancellationToken);
    }

    public async Task<AddStreamerResult> AddStreamerAsync(string usernameOrUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(usernameOrUrl))
        {
            return new AddStreamerResult(false, null, "Yayıncı adı veya kanal URL'si boş olamaz.");
        }

        var (username, channelUrl) = NormalizeStreamerInput(usernameOrUrl);

        if (string.IsNullOrWhiteSpace(username))
        {
            return new AddStreamerResult(false, null, "Geçersiz yayıncı adı.");
        }

        var existing = await _streamerRepository.GetByUsernameAsync(username, cancellationToken);
        if (existing != null)
        {
            _logger.LogWarning("Streamer addition rejected: {Username} already exists in database.", username);
            return new AddStreamerResult(false, null, $"'{username}' adındaki yayıncı zaten listede ekli.");
        }

        var streamer = new Streamer
        {
            Username = username,
            ChannelUrl = channelUrl,
            IsTrackingEnabled = true,
            CurrentStatus = StreamStatus.Unknown,
            CreatedAt = DateTime.Now
        };

        await _streamerRepository.AddAsync(streamer, cancellationToken);
        _logger.LogInformation("Successfully added streamer: {Username} ({ChannelUrl})", username, channelUrl);

        return new AddStreamerResult(true, streamer, null);
    }

    public async Task<bool> ToggleTrackingAsync(int streamerId, CancellationToken cancellationToken = default)
    {
        var streamer = await _streamerRepository.GetByIdAsync(streamerId, cancellationToken);
        if (streamer == null)
            return false;

        streamer.IsTrackingEnabled = !streamer.IsTrackingEnabled;
        await _streamerRepository.UpdateAsync(streamer, cancellationToken);
        _logger.LogInformation("Streamer {Username} tracking status updated to: {Status}", streamer.Username, streamer.IsTrackingEnabled);

        return true;
    }

    public async Task<bool> UpdatePreferredQualityAsync(int streamerId, string quality, CancellationToken cancellationToken = default)
    {
        var streamer = await _streamerRepository.GetByIdAsync(streamerId, cancellationToken);
        if (streamer == null)
            return false;

        streamer.PreferredQuality = string.IsNullOrWhiteSpace(quality) ? "En Yüksek (Kaynak)" : quality;
        await _streamerRepository.UpdateAsync(streamer, cancellationToken);
        _logger.LogInformation("Updated preferred quality for {Username} to {Quality}", streamer.Username, streamer.PreferredQuality);
        return true;
    }

    public async Task<bool> DeleteStreamerAsync(int streamerId, CancellationToken cancellationToken = default)
    {
        var streamer = await _streamerRepository.GetByIdAsync(streamerId, cancellationToken);
        if (streamer == null)
            return false;

        await _streamerRepository.DeleteAsync(streamerId, cancellationToken);
        _logger.LogInformation("Deleted streamer: {Username} (Id: {Id})", streamer.Username, streamerId);

        return true;
    }

    public async Task<Streamer?> CheckAndUpdateStreamerStatusAsync(int streamerId, CancellationToken cancellationToken = default)
    {
        var streamer = await _streamerRepository.GetByIdAsync(streamerId, cancellationToken);
        if (streamer == null)
            return null;

        var result = await _kickApiClient.CheckChannelStatusAsync(streamer.Username, cancellationToken);

        var previousStatus = streamer.CurrentStatus;
        streamer.CurrentStatus = result.Status;
        streamer.StreamTitle = result.StreamTitle;
        streamer.Category = result.Category;
        streamer.ViewerCount = result.ViewerCount;
        streamer.PlaybackUrl = result.PlaybackUrl;
        streamer.LastCheckedAt = DateTime.Now;

        if (result.Status == StreamStatus.Live)
        {
            if (previousStatus != StreamStatus.Live || streamer.LastLiveAt == null)
            {
                streamer.LastLiveAt = DateTime.Now;
            }
        }
        else if (result.Status == StreamStatus.Offline)
        {
            if (previousStatus == StreamStatus.Live || streamer.LastOfflineAt == null)
            {
                streamer.LastOfflineAt = DateTime.Now;
            }
        }

        await _streamerRepository.UpdateAsync(streamer, cancellationToken);
        return streamer;
    }

    public async Task CheckAndUpdateAllStatusesAsync(CancellationToken cancellationToken = default)
    {
        var list = await _streamerRepository.GetAllAsync(cancellationToken);
        foreach (var streamer in list)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (streamer.IsTrackingEnabled)
            {
                await CheckAndUpdateStreamerStatusAsync(streamer.Id, cancellationToken);
            }
        }
    }

    public (string NormalizedUsername, string ChannelUrl) NormalizeStreamerInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (string.Empty, string.Empty);

        var trimmed = input.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0)
            {
                var user = segments[0].TrimStart('@').Trim().ToLowerInvariant();
                return (user, $"https://kick.com/{user}");
            }
        }

        var cleanUsername = trimmed.TrimStart('@').Trim().ToLowerInvariant();
        return (cleanUsername, $"https://kick.com/{cleanUsername}");
    }
}
