namespace KickAutoRecorder.Core.Models;

public class RecordingSettings
{
    public string OutputFolder { get; set; } = @"C:\Users\kcuma\Desktop\anaklasor\projelerim\kick\kayitlar";
    public string? FFmpegPath { get; set; }
    public int PollingIntervalSeconds { get; set; } = 30;
    public long MinDiskSpaceGB { get; set; } = 5;
    public int MaxReconnectAttempts { get; set; } = 5;
    public string PreferredQuality { get; set; } = "En Yüksek (Kaynak)";
    public bool IsDarkTheme { get; set; } = true;
}
