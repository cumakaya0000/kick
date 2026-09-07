using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KickAutoRecorder.Core.Enums;

namespace KickAutoRecorder.Core.Entities;

public class YouTubeUploadJob : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public int Id { get; set; }
    public int? RenderJobId { get; set; }
    public int? RecordingLogId { get; set; }
    public string VideoFilePath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public string CategoryId { get; set; } = "20"; // 20 = Gaming
    public string DefaultLanguage { get; set; } = "tr";
    public string PrivacyStatus { get; set; } = "Private"; // Default SAFE Privacy
    public bool MadeForKids { get; set; }
    public string? PlaylistId { get; set; }

    private YouTubeUploadStatus _status = YouTubeUploadStatus.Pending;
    public YouTubeUploadStatus Status
    {
        get => _status;
        set { if (_status != value) { _status = value; OnPropertyChanged(); } }
    }

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        set { if (Math.Abs(_progressPercent - value) >= 0.01) { _progressPercent = value; OnPropertyChanged(); } }
    }

    private long _uploadedBytes;
    public long UploadedBytes
    {
        get => _uploadedBytes;
        set { if (_uploadedBytes != value) { _uploadedBytes = value; OnPropertyChanged(); } }
    }

    private long _totalBytes;
    public long TotalBytes
    {
        get => _totalBytes;
        set { if (_totalBytes != value) { _totalBytes = value; OnPropertyChanged(); } }
    }

    public string? UploadSessionUri { get; set; }
    public DateTime? UploadSessionCreatedAt { get; set; }
    public DateTime? LastProgressAt { get; set; }

    private string? _youTubeVideoId;
    public string? YouTubeVideoId
    {
        get => _youTubeVideoId;
        set { if (_youTubeVideoId != value) { _youTubeVideoId = value; OnPropertyChanged(); } }
    }

    private string? _youTubeVideoUrl;
    public string? YouTubeVideoUrl
    {
        get => _youTubeVideoUrl;
        set { if (_youTubeVideoUrl != value) { _youTubeVideoUrl = value; OnPropertyChanged(); } }
    }

    public int RetryCount { get; set; }

    private string? _lastError;
    public string? LastError
    {
        get => _lastError;
        set { if (_lastError != value) { _lastError = value; OnPropertyChanged(); } }
    }

    private string? _thumbnailWarning;
    public string? ThumbnailWarning
    {
        get => _thumbnailWarning;
        set { if (_thumbnailWarning != value) { _thumbnailWarning = value; OnPropertyChanged(); } }
    }

    private string? _playlistWarning;
    public string? PlaylistWarning
    {
        get => _playlistWarning;
        set { if (_playlistWarning != value) { _playlistWarning = value; OnPropertyChanged(); } }
    }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation Properties
    public RecordingLog? RecordingLog { get; set; }
    public VideoJob? RenderJob { get; set; }
}
