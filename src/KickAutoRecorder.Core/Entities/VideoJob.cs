using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KickAutoRecorder.Core.Enums;

namespace KickAutoRecorder.Core.Entities;

public class VideoJob : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public int Id { get; set; }
    public int RecordingLogId { get; set; }
    public string StreamerUsername { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Tags { get; set; }

    public string SegmentPathsJson { get; set; } = "[]";

    public double? TrimStartTimeSeconds { get; set; }
    public double? TrimEndTimeSeconds { get; set; }

    public string ExportPreset { get; set; } = "FastCopy";
    public string? TargetResolution { get; set; }
    public int? TargetFps { get; set; }
    public int AudioBitrateKbps { get; set; } = 256;

    public string OutputPath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public long FileSizeBytes { get; set; }

    private VideoJobStatus _status = VideoJobStatus.Pending;
    public VideoJobStatus Status
    {
        get => _status;
        set { if (_status != value) { _status = value; OnPropertyChanged(); } }
    }

    private double _progressPercentage;
    public double ProgressPercentage
    {
        get => _progressPercentage;
        set { if (Math.Abs(_progressPercentage - value) >= 0.1) { _progressPercentage = value; OnPropertyChanged(); } }
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set { if (_errorMessage != value) { _errorMessage = value; OnPropertyChanged(); } }
    }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public RecordingLog? RecordingLog { get; set; }
}
