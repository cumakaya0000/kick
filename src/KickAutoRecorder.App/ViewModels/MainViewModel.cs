using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KickAutoRecorder.App.Services;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IStreamerRepository _streamerRepository;
    private readonly IRecordingLogRepository _recordingLogRepository;
    private readonly IRecordingEngine _recordingEngine;
    private readonly IDiskService _diskService;
    private readonly IFFmpegLocator _ffmpegLocator;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _appName = "Kick";

    [ObservableProperty]
    private string _systemStatus = "Initializing Core Systems...";

    [ObservableProperty]
    private bool _isDatabaseConnected;

    [ObservableProperty]
    private bool _isLoggingActive = true;

    [ObservableProperty]
    private string _freeDiskSpaceText = "Calculating...";

    [ObservableProperty]
    private int _totalStreamersCount;

    [ObservableProperty]
    private int _liveStreamersCount;

    [ObservableProperty]
    private int _activeRecordingsCount;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    partial void OnIsDarkThemeChanged(bool value)
    {
        ThemeToggleText = value ? "🌙 Koyu Tema" : "☀️ Açık Tema";
        ThemeManager.ApplyTheme(value);
        if (SettingsViewModel != null && SettingsViewModel.IsDarkTheme != value)
        {
            SettingsViewModel.IsDarkTheme = value;
        }
    }

    [ObservableProperty]
    private string _themeToggleText = "🌙 Koyu Tema";

    [ObservableProperty]
    private int _autoRefreshCountdown = 5;

    private readonly System.Windows.Threading.DispatcherTimer _autoRefreshTimer;

    private string _ffmpegStatusText = "Checking...";
    public string FFmpegStatusText
    {
        get => _ffmpegStatusText;
        set => SetProperty(ref _ffmpegStatusText, value);
    }

    [ObservableProperty]
    private string _workerStatusText = "Monitoring Active";

    // Health Telemetry Monitor Properties
    [ObservableProperty]
    private bool _isHealthMonitorActive;

    [ObservableProperty]
    private string _healthStreamerUsername = "Henüz Aktif Kayıt Yok";

    [ObservableProperty]
    private string _healthDurationText = "00:00:00";

    [ObservableProperty]
    private string _healthFileSizeText = "0 MB";

    [ObservableProperty]
    private string _healthBitrateText = "0.0 Mbps";

    [ObservableProperty]
    private int _healthSegmentCount = 1;

    [ObservableProperty]
    private string _healthFFmpegState = "Idle";

    [ObservableProperty]
    private StreamersViewModel _streamersViewModel;

    [ObservableProperty]
    private RecordingsViewModel _recordingsViewModel;

    [ObservableProperty]
    private SettingsViewModel? _settingsViewModel;

    [ObservableProperty]
    private VideoStudioViewModel _videoStudioViewModel;

    [ObservableProperty]
    private int _selectedTabIndex = 0; // 0 = Dashboard, 1 = Streamers, 2 = Recordings, 3 = Settings, 4 = Studio

    public MainViewModel(
        IStreamerRepository streamerRepository,
        IRecordingLogRepository recordingLogRepository, 
        IRecordingEngine recordingEngine,
        IDiskService diskService,
        IFFmpegLocator ffmpegLocator,
        ISettingsService settingsService,
        StreamersViewModel streamersViewModel,
        RecordingsViewModel recordingsViewModel,
        SettingsViewModel settingsViewModel,
        VideoStudioViewModel videoStudioViewModel)
    {
        _streamerRepository = streamerRepository;
        _recordingLogRepository = recordingLogRepository;
        _recordingEngine = recordingEngine;
        _diskService = diskService;
        _ffmpegLocator = ffmpegLocator;
        _settingsService = settingsService;
        _streamersViewModel = streamersViewModel;
        _recordingsViewModel = recordingsViewModel;
        _settingsViewModel = settingsViewModel;
        _videoStudioViewModel = videoStudioViewModel;

        _recordingEngine.OnTelemetryUpdated += HandleTelemetryUpdated;

        if (_settingsViewModel != null)
        {
            _settingsViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.IsDarkTheme))
                {
                    IsDarkTheme = _settingsViewModel.IsDarkTheme;
                }
            };
        }

        _autoRefreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _autoRefreshTimer.Tick += (s, e) =>
        {
            AutoRefreshCountdown--;
            if (AutoRefreshCountdown <= 0)
            {
                AutoRefreshCountdown = 5;
                RefreshStatus();
            }
        };
        _autoRefreshTimer.Start();

        InitializeDashboard();
    }
    
    private void HandleTelemetryUpdated(RecordingHealthTelemetry telemetry)
    {
        if (telemetry == null) return;

        App.Current.Dispatcher.Invoke(() =>
        {
            IsHealthMonitorActive = telemetry.IsActive;
            HealthStreamerUsername = telemetry.StreamerUsername;
            HealthDurationText = telemetry.Duration.ToString(@"hh\:mm\:ss");
            HealthFileSizeText = $"{telemetry.FileSizeBytes / (1024.0 * 1024.0):N1} MB";
            HealthBitrateText = $"{telemetry.BitrateMbps:N1} Mbps";
            HealthSegmentCount = telemetry.SegmentCount;
            HealthFFmpegState = telemetry.FFmpegStatus;
        });
    }

    [RelayCommand]
    private void ExitApp()
    {
        try
        {
            // Arka plandaki işlemleri (özellikle FFmpeg derlemelerini) zorla sonlandır
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("ffmpeg"))
            {
                try { process.Kill(); } catch { }
            }
        }
        catch { }

        // Uygulamayı tamamen sonlandır
        Environment.Exit(0);
    }

    [RelayCommand]
    private void SelectTab(string tabIndexStr)
    {
        if (int.TryParse(tabIndexStr, out var index))
        {
            SelectedTabIndex = index;
            if (index == 0)
            {
                InitializeDashboard();
            }
            else if (index == 2)
            {
                _ = RecordingsViewModel.LoadLogsAsync();
            }
            else if (index == 4)
            {
                _ = VideoStudioViewModel.InitializeAsync();
            }
        }
    }

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        IsDarkTheme = !IsDarkTheme;
        ThemeToggleText = IsDarkTheme ? "🌙 Koyu Tema" : "☀️ Açık Tema";
        ThemeManager.ApplyTheme(IsDarkTheme);

        var settings = _settingsService.GetSettings();
        settings.IsDarkTheme = IsDarkTheme;
        await _settingsService.UpdateSettingsAsync(settings);

        if (SettingsViewModel != null)
        {
            SettingsViewModel.IsDarkTheme = IsDarkTheme;
        }
    }

    [RelayCommand]
    private void RefreshStatus()
    {
        AutoRefreshCountdown = 5;
        InitializeDashboard();
        _ = StreamersViewModel.LoadStreamersAsync();
    }

    private async void InitializeDashboard()
    {
        try
        {
            var settings = _settingsService.GetSettings();
            IsDarkTheme = settings.IsDarkTheme;
            ThemeToggleText = IsDarkTheme ? "🌙 Koyu Tema" : "☀️ Açık Tema";
            ThemeManager.ApplyTheme(IsDarkTheme);

            var streamers = (await _streamerRepository.GetAllAsync()).ToList();
            IsDatabaseConnected = true;

            TotalStreamersCount = streamers.Count;
            LiveStreamersCount = streamers.Count(s => s.CurrentStatus == Core.Enums.StreamStatus.Live);

            var activeLogs = (await _recordingLogRepository.GetActiveRecordingsAsync()).ToList();
            ActiveRecordingsCount = activeLogs.Count;

            var targetPath = settings.OutputFolder;
            if (!System.IO.Path.IsPathRooted(targetPath))
            {
                targetPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, targetPath);
            }

            var freeBytes = _diskService.GetAvailableFreeSpaceBytes(targetPath);
            FreeDiskSpaceText = $"{freeBytes / (1024.0 * 1024.0 * 1024.0):N1} GB Available";

            var ffmpegExe = _ffmpegLocator.FindFFmpegExecutable();
            FFmpegStatusText = !string.IsNullOrEmpty(ffmpegExe) ? "FFmpeg Ready" : "FFmpeg Missing";

            WorkerStatusText = "Monitoring Service Running";
            SystemStatus = "All Systems Operational (.NET 8 Host, SQLite, FFmpeg Engine, Serilog)";
        }
        catch (Exception ex)
        {
            IsDatabaseConnected = false;
            SystemStatus = $"Initialization Warning: {ex.Message}";
        }
    }

    public void Dispose()
    {
        _autoRefreshTimer?.Stop();
        if (_recordingEngine != null)
        {
            _recordingEngine.OnTelemetryUpdated -= HandleTelemetryUpdated;
        }
    }
}
