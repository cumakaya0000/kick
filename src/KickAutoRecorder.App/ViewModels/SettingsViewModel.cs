using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using KickAutoRecorder.App.Services;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IFFmpegLocator _ffmpegLocator;
    private readonly IYouTubeAuthService? _youTubeAuthService;

    [ObservableProperty]
    private string _outputFolder = "Recordings";

    [ObservableProperty]
    private bool _isYouTubeConnected;

    [ObservableProperty]
    private string _youTubeChannelTitle = string.Empty;

    [ObservableProperty]
    private string _youTubeStatusText = "Bağlı Değil";

    [ObservableProperty]
    private bool _enableAutoYouTubeUpload;

    private string _ffmpegPath = string.Empty;
    public string FFmpegPath
    {
        get => _ffmpegPath;
        set => SetProperty(ref _ffmpegPath, value);
    }

    [ObservableProperty]
    private int _pollingIntervalSeconds = 30;

    [ObservableProperty]
    private long _minDiskSpaceGB = 5;

    [ObservableProperty]
    private string _preferredQuality = "En Yüksek (Kaynak)";

    [ObservableProperty]
    private bool _isDarkTheme = true;

    partial void OnIsDarkThemeChanged(bool value)
    {
        ThemeManager.ApplyTheme(value);
    }

    public string[] QualityOptions { get; } = new[] { "En Yüksek (Kaynak)", "1080p", "720p", "480p", "360p" };

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _statusColor = "#10B981";

    [ObservableProperty]
    private string _logsContent = string.Empty;

    public SettingsViewModel(
        ISettingsService settingsService,
        IFFmpegLocator ffmpegLocator,
        IYouTubeAuthService? youTubeAuthService = null)
    {
        _settingsService = settingsService;
        _ffmpegLocator = ffmpegLocator;
        _youTubeAuthService = youTubeAuthService;

        LoadCurrentSettings();
        _ = RefreshLogsAsync();
        _ = RefreshYouTubeAccountStatusAsync();
    }

    private void LoadCurrentSettings()
    {
        var settings = _settingsService.GetSettings();
        OutputFolder = settings.OutputFolder;
        FFmpegPath = settings.FFmpegPath ?? string.Empty;
        PollingIntervalSeconds = settings.PollingIntervalSeconds;
        MinDiskSpaceGB = settings.MinDiskSpaceGB;
        PreferredQuality = string.IsNullOrWhiteSpace(settings.PreferredQuality) ? "En Yüksek (Kaynak)" : settings.PreferredQuality;
        IsDarkTheme = settings.IsDarkTheme;
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Kayıt Klasörünü Seçin",
            InitialDirectory = Directory.Exists(OutputFolder) ? OutputFolder : AppDomain.CurrentDomain.BaseDirectory
        };

        if (dialog.ShowDialog() == true)
        {
            OutputFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseFFmpegPath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "FFmpeg Çalıştırılabilir Dosyasını Seçin (ffmpeg.exe)",
            Filter = "FFmpeg Executable (ffmpeg.exe)|ffmpeg.exe|Tüm Dosyalar (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            FFmpegPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void AutoDetectFFmpeg()
    {
        var detected = _ffmpegLocator.FindFFmpegExecutable();
        if (!string.IsNullOrEmpty(detected))
        {
            FFmpegPath = detected;
            StatusMessage = $"FFmpeg tespit edildi: {detected}";
            StatusColor = "#10B981";
        }
        else
        {
            StatusMessage = "FFmpeg sistemde veya uygulama dizininde bulunamadı!";
            StatusColor = "#EF4444";
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new RecordingSettings
            {
                OutputFolder = string.IsNullOrWhiteSpace(OutputFolder) ? "Recordings" : OutputFolder.Trim(),
                FFmpegPath = string.IsNullOrWhiteSpace(FFmpegPath) ? null : FFmpegPath.Trim(),
                PollingIntervalSeconds = PollingIntervalSeconds <= 0 ? 30 : PollingIntervalSeconds,
                MinDiskSpaceGB = MinDiskSpaceGB <= 0 ? 5 : MinDiskSpaceGB,
                PreferredQuality = string.IsNullOrWhiteSpace(PreferredQuality) ? "En Yüksek (Kaynak)" : PreferredQuality,
                IsDarkTheme = IsDarkTheme
            };

            await _settingsService.UpdateSettingsAsync(settings);

            StatusMessage = "Ayarlar başarıyla kaydedildi ve uygulandı.";
            StatusColor = "#10B981";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ayarlar kaydedilirken hata oluştu: {ex.Message}";
            StatusColor = "#EF4444";
        }
    }

    [RelayCommand]
    private async Task RefreshLogsAsync()
    {
        try
        {
            var logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(logsDirectory))
            {
                LogsContent = "Henüz log kaydı oluşmadı.";
                return;
            }

            var logFiles = Directory.GetFiles(logsDirectory, "*.log");
            if (logFiles.Length == 0)
            {
                LogsContent = "Henüz log dosyası bulunamadı.";
                return;
            }

            var latestLogFile = logFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
            
            using var fs = new FileStream(latestLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var content = await reader.ReadToEndAsync();

            LogsContent = string.IsNullOrWhiteSpace(content) ? "Log dosyası boş." : content;
        }
        catch (Exception ex)
        {
            LogsContent = $"Loglar okunurken hata oluştu: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyLogsToClipboard()
    {
        if (string.IsNullOrWhiteSpace(LogsContent))
        {
            StatusMessage = "Kopyalanacak log kaydı bulunamadı.";
            StatusColor = "#EF4444";
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(LogsContent);
            StatusMessage = "Tüm log içerikleri başarıyla panoya kopyalandı!";
            StatusColor = "#10B981";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Loglar panoya kopyalanırken hata oluştu: {ex.Message}";
            StatusColor = "#EF4444";
        }
    }

    [RelayCommand]
    public async Task RefreshYouTubeAccountStatusAsync()
    {
        if (_youTubeAuthService == null) return;

        try
        {
            var info = await _youTubeAuthService.GetAccountInfoAsync();
            IsYouTubeConnected = info.IsConnected;
            YouTubeChannelTitle = info.ChannelTitle;
            YouTubeStatusText = info.StatusMessage;
        }
        catch (Exception ex)
        {
            IsYouTubeConnected = false;
            YouTubeStatusText = $"Hata: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ConnectYouTubeAccountAsync()
    {
        if (_youTubeAuthService == null) return;

        try
        {
            StatusMessage = "Google OAuth Yetkilendirme penceresi açılıyor...";
            StatusColor = "#38BDF8";
            var info = await _youTubeAuthService.ConnectAccountAsync();
            IsYouTubeConnected = info.IsConnected;
            YouTubeChannelTitle = info.ChannelTitle;
            YouTubeStatusText = info.StatusMessage;

            StatusMessage = $"✅ YouTube Hesabı Bağlandı: {info.ChannelTitle}";
            StatusColor = "#10B981";
        }
        catch (Exception ex)
        {
            StatusMessage = $"YouTube Bağlantı Hatası: {ex.Message}";
            StatusColor = "#EF4444";
        }
    }

    [RelayCommand]
    private async Task DisconnectYouTubeAccountAsync()
    {
        if (_youTubeAuthService == null) return;

        try
        {
            await _youTubeAuthService.DisconnectAccountAsync();
            IsYouTubeConnected = false;
            YouTubeChannelTitle = string.Empty;
            YouTubeStatusText = "Bağlı Değil";

            StatusMessage = "YouTube hesabı bağlantısı kesildi.";
            StatusColor = "#F59E0B";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Bağlantı Kesme Hatası: {ex.Message}";
            StatusColor = "#EF4444";
        }
    }

    [RelayCommand]
    private async Task TestYouTubeConnectionAsync()
    {
        if (_youTubeAuthService == null) return;

        try
        {
            StatusMessage = "YouTube bağlantısı ve kanal izinleri test ediliyor...";
            StatusColor = "#38BDF8";
            var info = await _youTubeAuthService.TestConnectionAsync();
            IsYouTubeConnected = info.IsConnected;
            YouTubeChannelTitle = info.ChannelTitle;
            YouTubeStatusText = info.StatusMessage;

            if (info.IsConnected)
            {
                StatusMessage = $"✅ YouTube Bağlantı Testi Başarılı! Kanal: {info.ChannelTitle}";
                StatusColor = "#10B981";
            }
            else
            {
                StatusMessage = "❌ YouTube Bağlantı Testi Başarısız. Hesabınız bağlı değil veya yetki süresi dolmuş.";
                StatusColor = "#EF4444";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Bağlantı Test Hatası: {ex.Message}";
            StatusColor = "#EF4444";
        }
    }
}
