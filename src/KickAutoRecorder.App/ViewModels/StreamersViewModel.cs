using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.App.ViewModels;

public partial class StreamersViewModel : ObservableObject
{
    private readonly IStreamerService _streamerService;
    private readonly IRecordingEngine _recordingEngine;
    private readonly IRecordingLogRepository _recordingLogRepository;
    private readonly ISettingsService _settingsService;
    private readonly IRecordingOrchestrator _recordingOrchestrator;

    [ObservableProperty]
    private string _inputUsernameOrUrl = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<StreamerItemViewModel> Streamers { get; } = new();

    private readonly System.Windows.Threading.DispatcherTimer _timer;

    public StreamersViewModel(
        IStreamerService streamerService,
        IRecordingEngine recordingEngine,
        IRecordingLogRepository recordingLogRepository,
        ISettingsService settingsService,
        IRecordingOrchestrator recordingOrchestrator)
    {
        _streamerService = streamerService;
        _recordingEngine = recordingEngine;
        _recordingLogRepository = recordingLogRepository;
        _settingsService = settingsService;
        _recordingOrchestrator = recordingOrchestrator;

        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (s, e) => UpdateTelemetryInfo();
        _timer.Start();

        _ = LoadStreamersAsync();
    }

    private void UpdateTelemetryInfo()
    {
        foreach (var item in Streamers)
        {
            item.IsRecordingActive = _recordingEngine.IsRecording(item.Id);
            if (item.IsRecordingActive)
            {
                var telemetry = _recordingEngine.GetActiveTelemetry(item.Id);
                if (telemetry != null)
                {
                    item.RecordingStartedAtText = telemetry.RecordingStartedAt.ToString("HH:mm:ss");
                    var duration = telemetry.Duration > TimeSpan.Zero 
                        ? telemetry.Duration 
                        : (DateTime.Now - telemetry.RecordingStartedAt);

                    item.RecordingDurationText = $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
                    item.IsRecovering = telemetry.IsRecovering;
                    item.ReconnectCount = telemetry.ReconnectCount;
                    item.FFmpegStatusText = telemetry.FFmpegStatus;
                    item.LastErrorText = telemetry.LastError ?? string.Empty;
                    item.NextRetryText = telemetry.NextRetrySeconds > 0 ? $"{telemetry.NextRetrySeconds}s" : string.Empty;
                }
            }
            else
            {
                item.RecordingStartedAtText = string.Empty;
                item.RecordingDurationText = string.Empty;
                item.IsRecovering = false;
                item.ReconnectCount = 0;
                item.FFmpegStatusText = "Idle";
                item.LastErrorText = string.Empty;
                item.NextRetryText = string.Empty;
            }
        }
    }

    [RelayCommand]
    public async Task LoadStreamersAsync()
    {
        IsLoading = true;
        try
        {
            Streamers.Clear();
            var list = await _streamerService.GetAllStreamersAsync();
            foreach (var item in list)
            {
                var vm = new StreamerItemViewModel(
                    item, 
                    ToggleTrackingAsync, 
                    DeleteStreamerAsync, 
                    CheckSingleStatusAsync,
                    StartManualRecordingAsync,
                    StopManualRecordingAsync,
                    UpdateQualityAsync);

                vm.IsRecordingActive = _recordingEngine.IsRecording(item.Id);
                Streamers.Add(vm);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CheckAllStatusesAsync()
    {
        IsLoading = true;
        try
        {
            await _streamerService.CheckAndUpdateAllStatusesAsync();
            await LoadStreamersAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task CheckSingleStatusAsync(int id)
    {
        var updated = await _streamerService.CheckAndUpdateStreamerStatusAsync(id);
        if (updated != null)
        {
            var item = Streamers.FirstOrDefault(s => s.Id == id);
            if (item != null)
            {
                item.UpdateFromEntity(updated);
                item.IsRecordingActive = _recordingEngine.IsRecording(updated.Id);
            }
        }
    }

    [RelayCommand]
    private async Task AddStreamerAsync()
    {
        HasError = false;
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(InputUsernameOrUrl))
        {
            ErrorMessage = "Lütfen bir yayıncı kullanıcı adı veya Kick kanal adresi girin.";
            HasError = true;
            return;
        }

        IsLoading = true;
        try
        {
            var result = await _streamerService.AddStreamerAsync(InputUsernameOrUrl);
            if (!result.IsSuccess || result.Streamer == null)
            {
                ErrorMessage = result.ErrorMessage ?? "Yayıncı eklenirken bilinmeyen bir hata oluştu.";
                HasError = true;
                return;
            }

            var itemVm = new StreamerItemViewModel(
                result.Streamer, 
                ToggleTrackingAsync, 
                DeleteStreamerAsync, 
                CheckSingleStatusAsync,
                StartManualRecordingAsync,
                StopManualRecordingAsync,
                UpdateQualityAsync);

            Streamers.Insert(0, itemVm);
            InputUsernameOrUrl = string.Empty;

            _ = CheckSingleStatusAsync(result.Streamer.Id);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UpdateQualityAsync(int id, string quality)
    {
        await _streamerService.UpdatePreferredQualityAsync(id, quality);
    }

    private async Task ToggleTrackingAsync(int id)
    {
        var success = await _streamerService.ToggleTrackingAsync(id);
        if (success)
        {
            var item = Streamers.FirstOrDefault(s => s.Id == id);
            if (item != null)
            {
                item.IsTrackingEnabled = !item.IsTrackingEnabled;
            }
        }
    }

    private async Task DeleteStreamerAsync(int id)
    {
        var success = await _streamerService.DeleteStreamerAsync(id);
        if (success)
        {
            var item = Streamers.FirstOrDefault(s => s.Id == id);
            if (item != null)
            {
                Streamers.Remove(item);
            }
        }
    }

    private async Task StartManualRecordingAsync(int id)
    {
        var item = Streamers.FirstOrDefault(s => s.Id == id);
        if (item == null) return;

        // Issue B Fix: Freshly check streamer status and playback URL before manual start
        var streamer = await _streamerService.CheckAndUpdateStreamerStatusAsync(id);

        if (streamer == null || string.IsNullOrWhiteSpace(streamer.PlaybackUrl))
        {
            ErrorMessage = $"{item.Username} için canlı yayın HLS adresi bulunamadı. Yayıncı offline olabilir.";
            HasError = true;
            return;
        }

        item.UpdateFromEntity(streamer);

        var outputFolder = _settingsService.GetSettings().OutputFolder;
        await _recordingEngine.StartRecordingAsync(streamer, streamer.PlaybackUrl, outputFolder);
        item.IsRecordingActive = true;
    }

    private async Task StopManualRecordingAsync(int id)
    {
        var item = Streamers.FirstOrDefault(s => s.Id == id);
        if (item == null) return;

        // Issue C Fix: Notify orchestrator of manual stop to prevent auto-restart on next monitoring tick
        _recordingOrchestrator.NotifyManualStop(id);

        var activeLogs = await _recordingLogRepository.GetActiveRecordingsAsync();
        var activeLog = activeLogs.FirstOrDefault(r => r.StreamerId == id);

        if (activeLog != null)
        {
            await _recordingEngine.StopRecordingAsync(activeLog.Id);
            item.IsRecordingActive = false;
        }
    }
}
