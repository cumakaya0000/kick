using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;

namespace KickAutoRecorder.App.ViewModels;

public partial class StreamerItemViewModel : ObservableObject
{
    private readonly Func<int, Task> _onToggleTracking;
    private readonly Func<int, Task> _onDelete;
    private readonly Func<int, Task> _onCheckStatus;
    private readonly Func<int, Task>? _onStartManualRecording;
    private readonly Func<int, Task>? _onStopManualRecording;
    private readonly Func<int, string, Task>? _onQualityChanged;

    public int Id { get; }

    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    private string _channelUrl;

    [ObservableProperty]
    private bool _isTrackingEnabled;

    [ObservableProperty]
    private StreamStatus _currentStatus;

    [ObservableProperty]
    private string _streamTitle;

    [ObservableProperty]
    private string _category;

    [ObservableProperty]
    private int _viewerCount;

    [ObservableProperty]
    private DateTime? _lastCheckedAt;

    [ObservableProperty]
    private DateTime? _lastLiveAt;

    [ObservableProperty]
    private DateTime? _lastOfflineAt;

    public string LastLiveAtText => LastLiveAt.HasValue ? LastLiveAt.Value.ToString("HH:mm:ss") : "-";
    public string LastOfflineAtText => LastOfflineAt.HasValue ? LastOfflineAt.Value.ToString("HH:mm:ss") : "-";
    public string LastCheckedAtText => LastCheckedAt.HasValue ? LastCheckedAt.Value.ToString("HH:mm:ss") : "-";

    [ObservableProperty]
    private bool _isRecordingActive;

    [ObservableProperty]
    private bool _isRecovering;

    [ObservableProperty]
    private int _reconnectCount;

    [ObservableProperty]
    private string _lastErrorText = string.Empty;

    [ObservableProperty]
    private string _nextRetryText = string.Empty;

    [ObservableProperty]
    private string _fFmpegStatusText = "Idle";

    [ObservableProperty]
    private string _recordingStartedAtText = string.Empty;

    [ObservableProperty]
    private string _recordingDurationText = string.Empty;

    [ObservableProperty]
    private string _preferredQuality = "En Yüksek (Kaynak)";

    public string[] QualityOptions { get; } = new[] { "En Yüksek (Kaynak)", "1080p", "720p", "480p", "360p" };

    public StreamerItemViewModel(
        Streamer streamer, 
        Func<int, Task> onToggleTracking, 
        Func<int, Task> onDelete,
        Func<int, Task> onCheckStatus,
        Func<int, Task>? onStartManualRecording = null,
        Func<int, Task>? onStopManualRecording = null,
        Func<int, string, Task>? onQualityChanged = null)
    {
        Id = streamer.Id;
        _username = streamer.Username;
        _channelUrl = streamer.ChannelUrl;
        _isTrackingEnabled = streamer.IsTrackingEnabled;
        _currentStatus = streamer.CurrentStatus;
        _streamTitle = streamer.StreamTitle;
        _category = streamer.Category;
        _viewerCount = streamer.ViewerCount;
        _lastCheckedAt = streamer.LastCheckedAt;
        _lastLiveAt = streamer.LastLiveAt;
        _lastOfflineAt = streamer.LastOfflineAt;
        _preferredQuality = string.IsNullOrWhiteSpace(streamer.PreferredQuality) ? "En Yüksek (Kaynak)" : streamer.PreferredQuality;

        _onToggleTracking = onToggleTracking;
        _onDelete = onDelete;
        _onCheckStatus = onCheckStatus;
        _onStartManualRecording = onStartManualRecording;
        _onStopManualRecording = onStopManualRecording;
        _onQualityChanged = onQualityChanged;
    }

    partial void OnPreferredQualityChanged(string value)
    {
        if (_onQualityChanged != null)
        {
            _ = _onQualityChanged(Id, value);
        }
    }

    public void UpdateFromEntity(Streamer streamer)
    {
        CurrentStatus = streamer.CurrentStatus;
        StreamTitle = streamer.StreamTitle;
        Category = streamer.Category;
        ViewerCount = streamer.ViewerCount;
        LastCheckedAt = streamer.LastCheckedAt;
        LastLiveAt = streamer.LastLiveAt;
        LastOfflineAt = streamer.LastOfflineAt;
        IsTrackingEnabled = streamer.IsTrackingEnabled;
        PreferredQuality = string.IsNullOrWhiteSpace(streamer.PreferredQuality) ? "En Yüksek (Kaynak)" : streamer.PreferredQuality;

        OnPropertyChanged(nameof(LastLiveAtText));
        OnPropertyChanged(nameof(LastOfflineAtText));
        OnPropertyChanged(nameof(LastCheckedAtText));
    }

    [RelayCommand]
    private async Task CheckStatusAsync()
    {
        await _onCheckStatus(Id);
    }

    [RelayCommand]
    private async Task ToggleTrackingAsync()
    {
        await _onToggleTracking(Id);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        await _onDelete(Id);
    }

    [RelayCommand]
    private async Task StartRecordingAsync()
    {
        if (_onStartManualRecording != null)
        {
            await _onStartManualRecording(Id);
        }
    }

    [RelayCommand]
    private async Task StopRecordingAsync()
    {
        if (_onStopManualRecording != null)
        {
            await _onStopManualRecording(Id);
        }
    }

    [RelayCommand]
    private void OpenChannelInBrowser()
    {
        try
        {
            var url = !string.IsNullOrWhiteSpace(ChannelUrl) ? ChannelUrl : $"https://kick.com/{Username}";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore browser launch errors
        }
    }
}
