using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.App.ViewModels;

public partial class VideoStudioViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<RecordingLog> _recordings = new();

    [ObservableProperty]
    private RecordingLog? _selectedRecording;

    [ObservableProperty]
    private Uri? _videoSourceUri;

    [ObservableProperty]
    private double _currentPositionSeconds;

    [ObservableProperty]
    private string _currentPositionText = "00:00:00";

    [ObservableProperty]
    private double _totalDurationSeconds;

    [ObservableProperty]
    private string _totalDurationText = "00:00:00";

    [ObservableProperty]
    private double _trimStartSeconds;

    [ObservableProperty]
    private double _trimEndSeconds;

    [ObservableProperty]
    private double _selectedSectionDurationSeconds;

    [ObservableProperty]
    private string _selectedSectionDurationText = "00:00:00";

    [ObservableProperty]
    private ObservableCollection<VideoExportProfile> _exportProfiles = new();

    [ObservableProperty]
    private VideoExportProfile? _selectedProfile;

    // YouTube Metadata Properties
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _tags = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _categories = new() { "Gaming", "Entertainment", "People & Blogs", "Education", "Science & Technology" };

    [ObservableProperty]
    private string _category = "Gaming";

    [ObservableProperty]
    private ObservableCollection<string> _languages = new() { "Turkish", "English", "German", "Spanish" };

    [ObservableProperty]
    private string _language = "Turkish";

    [ObservableProperty]
    private string _gameName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _privacyStatuses = new() { "Private", "Unlisted", "Public" };

    [ObservableProperty]
    private string _privacyStatus = "Private";

    [ObservableProperty]
    private string _playlistName = string.Empty;

    [ObservableProperty]
    private bool _madeForKids;

    [ObservableProperty]
    private string _titleLengthText = "0/100";

    [ObservableProperty]
    private string _descriptionLengthText = "0/5000";

    [ObservableProperty]
    private string _outputFileName = string.Empty;

    [ObservableProperty]
    private string _outputFolder = string.Empty;

    [ObservableProperty]
    private string? _thumbnailPath;

    [ObservableProperty]
    private string _selectedThumbnailFormat = "JPEG"; // "JPEG" or "PNG"

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private ObservableCollection<VideoJob> _jobs = new();

    private readonly IRecordingLogRepository _logRepository;
    private readonly IVideoJobRepository _jobRepository;
    private readonly IYouTubeMetadataDraftRepository _draftRepository;
    private readonly IVideoPreparationService _preparationService;
    private readonly IThumbnailGeneratorService _thumbnailGenerator;
    private readonly IVideoJobQueue _jobQueue;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<VideoStudioViewModel> _logger;
    private readonly IYouTubeUploadRepository? _uploadRepository;
    private readonly IYouTubeUploadQueue? _uploadQueue;
    private readonly IYouTubeUploadService? _uploadService;
    private readonly IYouTubeMetadataMapper? _metadataMapper;

    [ObservableProperty]
    private ObservableCollection<YouTubeUploadJob> _uploadJobs = new();

    public VideoStudioViewModel(
        IRecordingLogRepository logRepository,
        IVideoJobRepository jobRepository,
        IYouTubeMetadataDraftRepository draftRepository,
        IVideoPreparationService preparationService,
        IThumbnailGeneratorService thumbnailGenerator,
        IVideoJobQueue jobQueue,
        ISettingsService settingsService,
        ILogger<VideoStudioViewModel> logger,
        IYouTubeUploadRepository? uploadRepository = null,
        IYouTubeUploadQueue? uploadQueue = null,
        IYouTubeUploadService? uploadService = null,
        IYouTubeMetadataMapper? metadataMapper = null)
    {
        _logRepository = logRepository;
        _jobRepository = jobRepository;
        _draftRepository = draftRepository;
        _preparationService = preparationService;
        _thumbnailGenerator = thumbnailGenerator;
        _jobQueue = jobQueue;
        _settingsService = settingsService;
        _logger = logger;
        _uploadRepository = uploadRepository;
        _uploadQueue = uploadQueue;
        _uploadService = uploadService;
        _metadataMapper = metadataMapper;

        ExportProfiles = new ObservableCollection<VideoExportProfile>
        {
            VideoExportProfile.FastCopy,
            VideoExportProfile.Accurate1080p60,
            VideoExportProfile.YouTube4K
        };
        SelectedProfile = ExportProfiles.First();

        OutputFolder = Path.Combine(_settingsService.GetSettings().OutputFolder, "Exports");
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            // 1. Aşama: Toplam DB kaydı
            var allLogs = (await _logRepository.GetAllAsync()).ToList();
            _logger.LogDebug("Serilog Debug [VideoStudio] Aşama 1: Toplam DB kaydı sayısı: {Count}", allLogs.Count);

            // 2. Aşama: Uygun durumdaki kayıtlar (Completed, Interrupted, Recovered, Cancelled, Failed, Recording)
            var validStatusLogs = allLogs.Where(l =>
                l.Status == RecordingStatus.Completed ||
                l.Status == RecordingStatus.Interrupted ||
                l.Status == RecordingStatus.Recovered ||
                l.Status == RecordingStatus.Cancelled ||
                l.Status == RecordingStatus.Failed ||
                l.Status == RecordingStatus.Recording).ToList();

            _logger.LogDebug("Serilog Debug [VideoStudio] Aşama 2: Uygun durumdaki kayıt sayısı: {Count} (Elendi: {Filtered})",
                validStatusLogs.Count, allLogs.Count - validStatusLogs.Count);

            // 3. Aşama: Mevcut dosyası/segmentleri bulunan kayıtlar
            var existingFileLogs = new List<RecordingLog>();
            foreach (var log in validStatusLogs)
            {
                if (HasExistingFileOrSegments(log))
                {
                    existingFileLogs.Add(log);
                }
                else
                {
                    _logger.LogWarning("Serilog Debug [VideoStudio] Diskte video dosyası veya segmentleri bulunmayan kayıt atlandı: ID {Id}, Streamer '{Streamer}', Yol '{FilePath}'",
                        log.Id, log.StreamerUsername, log.FilePath);
                }
            }
            _logger.LogDebug("Serilog Debug [VideoStudio] Aşama 3: Mevcut dosyası bulunan kayıt sayısı: {Count} (Bulunamayıp Atlanan: {Skipped})",
                existingFileLogs.Count, validStatusLogs.Count - existingFileLogs.Count);

            // 4. Aşama: UI listesine eklenen kayıt sayısı ve UI Thread Dispatcher güncellemesi
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() =>
                {
                    Recordings.Clear();
                    foreach (var log in existingFileLogs)
                    {
                        Recordings.Add(log);
                    }
                });
            }
            else
            {
                Recordings.Clear();
                foreach (var log in existingFileLogs)
                {
                    Recordings.Add(log);
                }
            }
            _logger.LogDebug("Serilog Debug [VideoStudio] Aşama 4: UI listesine eklenen kayıt sayısı: {Count}", Recordings.Count);

            var existingJobs = await _jobRepository.GetAllAsync();
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() =>
                {
                    Jobs.Clear();
                    foreach (var job in existingJobs)
                    {
                        Jobs.Add(job);
                    }
                });
            }
            else
            {
                Jobs.Clear();
                foreach (var job in existingJobs)
                {
                    Jobs.Add(job);
                }
            }

            if (_uploadRepository != null)
            {
                var existingUploadJobs = await _uploadRepository.GetAllAsync();
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() =>
                    {
                        UploadJobs.Clear();
                        foreach (var uploadJob in existingUploadJobs)
                        {
                            UploadJobs.Add(uploadJob);
                        }
                    });
                }
                else
                {
                    UploadJobs.Clear();
                    foreach (var uploadJob in existingUploadJobs)
                    {
                        UploadJobs.Add(uploadJob);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while initializing VideoStudioViewModel");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool HasExistingFileOrSegments(RecordingLog log)
    {
        if (string.IsNullOrWhiteSpace(log.FilePath))
        {
            return false;
        }

        // 1. Doğrudan dosya varlık kontrolü
        if (File.Exists(log.FilePath))
        {
            return true;
        }

        // 2. Çoklu segment (multi-segment) veya session klasörü varlık kontrolü
        try
        {
            var sessionDirectory = Path.GetDirectoryName(log.FilePath);
            if (!string.IsNullOrEmpty(sessionDirectory) && Directory.Exists(sessionDirectory))
            {
                var manifestPath = Path.Combine(sessionDirectory, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    return true;
                }

                var mp4Files = Directory.GetFiles(sessionDirectory, "*.mp4");
                if (mp4Files.Length > 0)
                {
                    return true;
                }

                var tsFiles = Directory.GetFiles(sessionDirectory, "*.ts");
                if (tsFiles.Length > 0)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignored file access exception
        }

        return false;
    }

    partial void OnTitleChanged(string value)
    {
        var len = value != null ? value.Length : 0;
        TitleLengthText = $"{len}/100";
    }

    partial void OnDescriptionChanged(string value)
    {
        var len = value != null ? value.Length : 0;
        DescriptionLengthText = $"{len}/5000";
    }

    partial void OnSelectedRecordingChanged(RecordingLog? value)
    {
        if (value != null)
        {
            _ = OnRecordingSelectedAsync(value);
        }
        else
        {
            ResetSelection();
        }
    }

    [RelayCommand]
    public void ClearSelection()
    {
        SelectedRecording = null;
    }

    private void ResetSelection()
    {
        VideoSourceUri = null;
        CurrentPositionSeconds = 0;
        CurrentPositionText = "00:00:00";
        TotalDurationSeconds = 0;
        TotalDurationText = "00:00:00";
        TrimStartSeconds = 0;
        TrimEndSeconds = 0;
        SelectedSectionDurationSeconds = 0;
        SelectedSectionDurationText = "00:00:00";

        Title = string.Empty;
        Description = string.Empty;
        Tags = string.Empty;
        Category = "Gaming";
        Language = "Turkish";
        GameName = string.Empty;
        PrivacyStatus = "Private";
        PlaylistName = string.Empty;
        MadeForKids = false;
        ThumbnailPath = null;
        OutputFileName = string.Empty;

        StatusMessage = "Kayıt seçimi temizlendi (Geri adım atıldı).";
    }

    [RelayCommand]
    public async Task RefreshRecordingsAsync()
    {
        await InitializeAsync();
        StatusMessage = "Kayıt listesi tekrar yüklendi.";
    }

    private async Task OnRecordingSelectedAsync(RecordingLog log)
    {
        IsBusy = true;
        StatusMessage = "Kayıt segmentleri ve YouTube taslağı yükleniyor...";
        try
        {
            if (File.Exists(log.FilePath))
            {
                VideoSourceUri = new Uri(log.FilePath);
            }
            else
            {
                VideoSourceUri = null;
            }

            // Check if existing YouTubeMetadataDraft is saved in DB for this recording
            var draft = await _draftRepository.GetByRecordingLogIdAsync(log.Id);
            if (draft != null)
            {
                Title = draft.Title;
                Description = draft.Description;
                Tags = draft.Tags;
                Category = draft.Category;
                Language = draft.Language;
                GameName = draft.GameName ?? string.Empty;
                PrivacyStatus = draft.PrivacyStatus;
                PlaylistName = draft.PlaylistName ?? string.Empty;
                MadeForKids = draft.MadeForKids;
                if (!string.IsNullOrEmpty(draft.ThumbnailPath) && File.Exists(draft.ThumbnailPath))
                {
                    ThumbnailPath = draft.ThumbnailPath;
                }
            }
            else
            {
                Title = $"{log.StreamerUsername} - {log.StreamTitle} ({log.StartedAt:dd.MM.yyyy})";
                Description = $"Original Stream: {log.StreamTitle}\nStreamer: {log.StreamerUsername}\nRecorded on: {log.StartedAt}";
                Tags = $"{log.StreamerUsername}, kick, live stream, gaming, clip";
                Category = "Gaming";
                Language = "Turkish";
                PrivacyStatus = "Private";
                MadeForKids = false;
            }

            var sanitizedTitle = string.Join("_", Title.Split(Path.GetInvalidFileNameChars()));
            OutputFileName = $"{sanitizedTitle}.mp4";

            var prep = await _preparationService.PrepareSegmentsAsync(log.Id);
            if (prep.Success)
            {
                // Multi-segment fallback for player URI if log.FilePath doesn't exist directly
                if (VideoSourceUri == null && prep.ClosedSegmentPaths.Count > 0 && File.Exists(prep.ClosedSegmentPaths.First()))
                {
                    VideoSourceUri = new Uri(prep.ClosedSegmentPaths.First());
                }

                TotalDurationSeconds = log.Duration.HasValue ? log.Duration.Value.TotalSeconds : 3600;
                TotalDurationText = TimeSpan.FromSeconds(TotalDurationSeconds).ToString(@"hh\:mm\:ss");

                TrimStartSeconds = 0;
                TrimEndSeconds = TotalDurationSeconds;
                UpdateSelectedSectionDuration();

                StatusMessage = $"{prep.ClosedSegmentPaths.Count} segment hazırlandı. Süre: {TotalDurationText}";
            }
            else
            {
                StatusMessage = $"Uyarı: {prep.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
            _logger.LogError(ex, "Failed to load recording ID {LogId}", log.Id);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnTrimStartSecondsChanged(double value) => UpdateSelectedSectionDuration();
    partial void OnTrimEndSecondsChanged(double value) => UpdateSelectedSectionDuration();

    private void UpdateSelectedSectionDuration()
    {
        var dur = Math.Max(0, TrimEndSeconds - TrimStartSeconds);
        SelectedSectionDurationSeconds = dur;
        SelectedSectionDurationText = TimeSpan.FromSeconds(dur).ToString(@"hh\:mm\:ss");
    }

    [RelayCommand]
    private void SetTrimStartFromCurrentPosition()
    {
        if (CurrentPositionSeconds < TrimEndSeconds)
        {
            TrimStartSeconds = CurrentPositionSeconds;
            StatusMessage = $"Başlangıç zamanı ayarlandı: {TimeSpan.FromSeconds(TrimStartSeconds):hh\\:mm\\:ss}";
        }
        else
        {
            StatusMessage = "Hata: Başlangıç zamanı bitiş zamanından büyük olamaz!";
        }
    }

    [RelayCommand]
    private void SetTrimEndFromCurrentPosition()
    {
        if (CurrentPositionSeconds > TrimStartSeconds)
        {
            TrimEndSeconds = CurrentPositionSeconds;
            StatusMessage = $"Bitiş zamanı ayarlandı: {TimeSpan.FromSeconds(TrimEndSeconds):hh\\:mm\\:ss}";
        }
        else
        {
            StatusMessage = "Hata: Bitiş zamanı başlangıç zamanından küçük olamaz!";
        }
    }

    [RelayCommand]
    private async Task SaveDraftAsync()
    {
        if (SelectedRecording == null)
        {
            StatusMessage = "Lütfen önce bir kayıt seçin.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            StatusMessage = "Hata: YouTube video başlığı boş olamaz!";
            return;
        }

        if (Title.Length > 100)
        {
            StatusMessage = "Hata: YouTube başlığı 100 karakteri geçemez!";
            return;
        }

        IsBusy = true;
        try
        {
            var draft = new YouTubeVideoDraft
            {
                RecordingLogId = SelectedRecording.Id,
                StreamerUsername = SelectedRecording.StreamerUsername,
                Title = Title,
                Description = Description,
                Tags = Tags,
                Category = Category,
                Language = Language,
                GameName = GameName,
                ThumbnailPath = ThumbnailPath,
                PrivacyStatus = PrivacyStatus,
                PlaylistName = PlaylistName,
                MadeForKids = MadeForKids
            };

            await _draftRepository.SaveOrUpdateAsync(draft);
            StatusMessage = "✅ YouTube Metadata Taslağı SQLite veritabanına kaydedildi!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Taslak Kayıt Hatası: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ExportDraftJson()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            StatusMessage = "Hata: Başlığı boş olan taslak dışa aktarılamaz.";
            return;
        }

        var draftData = new
        {
            Title,
            Description,
            Tags,
            Category,
            Language,
            GameName,
            ThumbnailPath,
            PrivacyStatus,
            PlaylistName,
            MadeForKids,
            ExportedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(draftData, new JsonSerializerOptions { WriteIndented = true });

        var saveFileDialog = new SaveFileDialog
        {
            Title = "YouTube Metadata Taslağını JSON Olarak Dışa Aktar",
            Filter = "JSON Dosyaları (*.json)|*.json",
            FileName = $"{string.Join("_", Title.Split(Path.GetInvalidFileNameChars()))}_metadata.json"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            File.WriteAllText(saveFileDialog.FileName, json);
            StatusMessage = $"Taslak başarıyla dışa aktarıldı: {Path.GetFileName(saveFileDialog.FileName)}";
        }
    }

    [RelayCommand]
    private async Task GrabFrameThumbnailAsync()
    {
        if (SelectedRecording == null || string.IsNullOrEmpty(SelectedRecording.FilePath))
        {
            StatusMessage = "Lütfen önce bir kayıt seçin.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Videonun karesinden 1280x720 thumbnail oluşturuluyor...";
        try
        {
            var outputDir = Path.Combine(_settingsService.GetSettings().OutputFolder, "Thumbnails");
            var format = SelectedThumbnailFormat.ToLowerInvariant();
            var path = await _thumbnailGenerator.GenerateThumbnailAsync(
                SelectedRecording.FilePath, CurrentPositionSeconds, outputDir, format, 1280, 720);

            if (!string.IsNullOrEmpty(path))
            {
                ThumbnailPath = path;
                StatusMessage = "1280x720 Thumbnail karesi yakalandı!";
            }
            else
            {
                StatusMessage = "Thumbnail oluşturulamadı.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Thumbnail Hatası: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BrowseCustomThumbnail()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Özel Görsel Seç (Thumbnail)",
            Filter = "Görsel Dosyaları (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|Tüm Dosyalar (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            ThumbnailPath = openFileDialog.FileName;
            StatusMessage = $"Özel görsel seçildi: {Path.GetFileName(ThumbnailPath)}";
        }
    }

    [RelayCommand]
    private async Task EnqueueRenderJobAsync()
    {
        if (SelectedRecording == null)
        {
            StatusMessage = "Lütfen önce bir kayıt seçin.";
            return;
        }

        if (SelectedProfile == null)
        {
            StatusMessage = "Lütfen bir export profili seçin.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            StatusMessage = "Hata: Video başlığı boş olamaz!";
            return;
        }

        if (Title.Length > 100)
        {
            StatusMessage = "Hata: Video başlığı 100 karakteri geçemez!";
            return;
        }

        if (TrimStartSeconds >= TrimEndSeconds || SelectedSectionDurationSeconds <= 0)
        {
            StatusMessage = "Hata: Başlangıç zamanı bitiş zamanından büyük/eşit olamaz!";
            return;
        }

        IsBusy = true;
        StatusMessage = "İş doğrulanıyor...";
        try
        {
            var prep = await _preparationService.PrepareSegmentsAsync(SelectedRecording.Id);
            if (!prep.Success || prep.ClosedSegmentPaths.Count == 0)
            {
                StatusMessage = $"Kuyruğa eklenemedi: {prep.ErrorMessage}";
                return;
            }

            if (SelectedProfile.RequiresNative4KSource && !prep.IsSource4K)
            {
                StatusMessage = "Hata: YouTube 4K profili yalnızca kaynak video 4K (3840x2160) olduğunda seçilebilir!";
                return;
            }

            var fullOutputPath = Path.Combine(OutputFolder, OutputFileName);

            foreach (var seg in prep.ClosedSegmentPaths)
            {
                if (string.Equals(Path.GetFullPath(seg), Path.GetFullPath(fullOutputPath), StringComparison.OrdinalIgnoreCase))
                {
                    StatusMessage = "Hata: Çıktı dosya adı kaynak kayıt dosyası ile aynı olamaz!";
                    return;
                }
            }

            var job = new VideoJob
            {
                RecordingLogId = SelectedRecording.Id,
                StreamerUsername = SelectedRecording.StreamerUsername,
                Title = Title,
                Description = Description,
                Tags = Tags,
                SegmentPathsJson = System.Text.Json.JsonSerializer.Serialize(prep.ClosedSegmentPaths),
                TrimStartTimeSeconds = TrimStartSeconds > 0 ? TrimStartSeconds : null,
                TrimEndTimeSeconds = TrimEndSeconds < TotalDurationSeconds ? TrimEndSeconds : null,
                ExportPreset = SelectedProfile.Name,
                TargetResolution = SelectedProfile.Resolution,
                TargetFps = SelectedProfile.Fps,
                AudioBitrateKbps = SelectedProfile.AudioBitrateKbps,
                OutputPath = fullOutputPath,
                ThumbnailPath = ThumbnailPath,
                Status = VideoJobStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await _jobRepository.AddAsync(job);

            // Save YouTube metadata draft and associate with this VideoJob
            var draft = new YouTubeVideoDraft
            {
                RecordingLogId = SelectedRecording.Id,
                VideoJobId = job.Id,
                StreamerUsername = SelectedRecording.StreamerUsername,
                Title = Title,
                Description = Description,
                Tags = Tags,
                Category = Category,
                Language = Language,
                GameName = GameName,
                ThumbnailPath = ThumbnailPath,
                PrivacyStatus = PrivacyStatus,
                PlaylistName = PlaylistName,
                MadeForKids = MadeForKids
            };
            await _draftRepository.SaveOrUpdateAsync(draft);

            await _jobQueue.EnqueueAsync(job.Id);

            Jobs.Insert(0, job);
            StatusMessage = $"'{job.Title}' video işleme kuyruğuna eklendi ve metadata taslağı kaydedildi!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelJobAsync(VideoJob? job)
    {
        if (job == null) return;

        if (_jobQueue.TryCancel(job.Id))
        {
            StatusMessage = $"İş (ID: {job.Id}) iptal ediliyor...";
        }
        else
        {
            await _jobRepository.UpdateStatusAsync(job.Id, VideoJobStatus.Cancelled, "Cancelled by user");
            job.Status = VideoJobStatus.Cancelled;
            StatusMessage = $"İş (ID: {job.Id}) iptal edildi.";
        }
    }

    [RelayCommand]
    private async Task EnqueueRenderAndUploadAsync()
    {
        if (SelectedRecording == null)
        {
            StatusMessage = "Lütfen önce bir kayıt seçin.";
            return;
        }

        if (SelectedProfile == null)
        {
            StatusMessage = "Lütfen bir export profili seçin.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            StatusMessage = "Hata: Video başlığı boş olamaz!";
            return;
        }

        if (Title.Length > 100)
        {
            StatusMessage = "Hata: Video başlığı 100 karakteri geçemez!";
            return;
        }

        if (TrimStartSeconds >= TrimEndSeconds || SelectedSectionDurationSeconds <= 0)
        {
            StatusMessage = "Hata: Başlangıç zamanı bitiş zamanından büyük/eşit olamaz!";
            return;
        }

        IsBusy = true;
        StatusMessage = "İş doğrulanıyor...";
        try
        {
            var prep = await _preparationService.PrepareSegmentsAsync(SelectedRecording.Id);
            if (!prep.Success || prep.ClosedSegmentPaths.Count == 0)
            {
                StatusMessage = $"Kuyruğa eklenemedi: {prep.ErrorMessage}";
                return;
            }

            if (SelectedProfile.RequiresNative4KSource && !prep.IsSource4K)
            {
                StatusMessage = "Hata: YouTube 4K profili yalnızca kaynak video 4K (3840x2160) olduğunda seçilebilir!";
                return;
            }

            var fullOutputPath = Path.Combine(OutputFolder, OutputFileName);

            foreach (var seg in prep.ClosedSegmentPaths)
            {
                if (string.Equals(Path.GetFullPath(seg), Path.GetFullPath(fullOutputPath), StringComparison.OrdinalIgnoreCase))
                {
                    StatusMessage = "Hata: Çıktı dosya adı kaynak kayıt dosyası ile aynı olamaz!";
                    return;
                }
            }

            var job = new VideoJob
            {
                RecordingLogId = SelectedRecording.Id,
                StreamerUsername = SelectedRecording.StreamerUsername,
                Title = Title,
                Description = Description,
                Tags = Tags,
                SegmentPathsJson = System.Text.Json.JsonSerializer.Serialize(prep.ClosedSegmentPaths),
                TrimStartTimeSeconds = TrimStartSeconds > 0 ? TrimStartSeconds : null,
                TrimEndTimeSeconds = TrimEndSeconds < TotalDurationSeconds ? TrimEndSeconds : null,
                ExportPreset = SelectedProfile.Name,
                TargetResolution = SelectedProfile.Resolution,
                TargetFps = SelectedProfile.Fps,
                AudioBitrateKbps = SelectedProfile.AudioBitrateKbps,
                OutputPath = fullOutputPath,
                ThumbnailPath = ThumbnailPath,
                Status = VideoJobStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await _jobRepository.AddAsync(job);

            // Save draft
            var draft = new YouTubeVideoDraft
            {
                RecordingLogId = SelectedRecording.Id,
                VideoJobId = job.Id,
                StreamerUsername = SelectedRecording.StreamerUsername,
                Title = Title,
                Description = Description,
                Tags = Tags,
                Category = Category,
                Language = Language,
                GameName = GameName,
                ThumbnailPath = ThumbnailPath,
                PrivacyStatus = PrivacyStatus,
                PlaylistName = PlaylistName,
                MadeForKids = MadeForKids
            };
            await _draftRepository.SaveOrUpdateAsync(draft);

            // Create linked YouTubeUploadJob (Status: WaitingForRender)
            if (_uploadRepository != null)
            {
                var categoryId = _metadataMapper != null ? _metadataMapper.MapCategoryToCategoryId(Category) : "20";
                var langCode = _metadataMapper != null ? _metadataMapper.MapLanguageToLanguageCode(Language) : "tr";

                var uploadJob = new YouTubeUploadJob
                {
                    RenderJobId = job.Id,
                    RecordingLogId = SelectedRecording.Id,
                    VideoFilePath = fullOutputPath,
                    ThumbnailPath = ThumbnailPath,
                    Title = Title,
                    Description = Description,
                    Tags = Tags,
                    CategoryId = categoryId,
                    DefaultLanguage = langCode,
                    PrivacyStatus = string.IsNullOrWhiteSpace(PrivacyStatus) ? "Private" : PrivacyStatus,
                    MadeForKids = MadeForKids,
                    PlaylistId = PlaylistName,
                    Status = YouTubeUploadStatus.WaitingForRender,
                    CreatedAt = DateTime.UtcNow
                };

                await _uploadRepository.AddAsync(uploadJob);
                UploadJobs.Insert(0, uploadJob);
            }

            await _jobQueue.EnqueueAsync(job.Id);
            Jobs.Insert(0, job);

            StatusMessage = $"'{job.Title}' render ve YouTube yükleme kuyruğuna eklendi! Render tamamlandıktan sonra otomatik yüklenecektir.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EnqueueUploadOnlyAsync()
    {
        if (SelectedRecording == null && string.IsNullOrEmpty(VideoSourceUri?.LocalPath))
        {
            StatusMessage = "Lütfen önce bir kayıt seçin.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            StatusMessage = "Hata: Video başlığı boş olamaz!";
            return;
        }

        if (_uploadRepository == null || _uploadQueue == null)
        {
            StatusMessage = "YouTube yükleme servisi aktif değil.";
            return;
        }

        IsBusy = true;
        try
        {
            var filePath = SelectedRecording?.FilePath ?? VideoSourceUri?.LocalPath ?? "";
            if (!File.Exists(filePath))
            {
                StatusMessage = "Hata: Yüklenecek video dosyası diskte bulunamadı.";
                return;
            }

            var categoryId = _metadataMapper != null ? _metadataMapper.MapCategoryToCategoryId(Category) : "20";
            var langCode = _metadataMapper != null ? _metadataMapper.MapLanguageToLanguageCode(Language) : "tr";

            var uploadJob = new YouTubeUploadJob
            {
                RecordingLogId = SelectedRecording?.Id,
                VideoFilePath = filePath,
                ThumbnailPath = ThumbnailPath,
                Title = Title,
                Description = Description,
                Tags = Tags,
                CategoryId = categoryId,
                DefaultLanguage = langCode,
                PrivacyStatus = string.IsNullOrWhiteSpace(PrivacyStatus) ? "Private" : PrivacyStatus,
                MadeForKids = MadeForKids,
                PlaylistId = PlaylistName,
                Status = YouTubeUploadStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await _uploadRepository.AddAsync(uploadJob);
            await _uploadQueue.EnqueueAsync(uploadJob.Id);

            UploadJobs.Insert(0, uploadJob);
            StatusMessage = $"'{uploadJob.Title}' YouTube yükleme kuyruğuna eklendi!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelUploadJobAsync(YouTubeUploadJob? job)
    {
        if (job == null) return;

        if (_uploadQueue != null && _uploadQueue.TryCancel(job.Id))
        {
            StatusMessage = $"YouTube Yüklemesi (ID: {job.Id}) iptal ediliyor...";
        }
        else if (_uploadRepository != null)
        {
            await _uploadRepository.UpdateStatusAsync(job.Id, YouTubeUploadStatus.Cancelled, "Cancelled by user");
            job.Status = YouTubeUploadStatus.Cancelled;
            StatusMessage = $"YouTube Yüklemesi (ID: {job.Id}) iptal edildi.";
        }
    }

    [RelayCommand]
    private async Task RetryUploadJobAsync(YouTubeUploadJob? job)
    {
        if (job == null || _uploadRepository == null || _uploadQueue == null) return;

        job.Status = YouTubeUploadStatus.Pending;
        job.LastError = null;
        await _uploadRepository.UpdateAsync(job);
        await _uploadQueue.EnqueueAsync(job.Id);
        StatusMessage = $"YouTube Yükleme İşlemi (ID: {job.Id}) tekrar kuyruğa alındı.";
    }

    [RelayCommand]
    private void OpenYouTubeVideo(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private async Task RefreshJobsAsync()
    {
        await InitializeAsync();
    }
}
