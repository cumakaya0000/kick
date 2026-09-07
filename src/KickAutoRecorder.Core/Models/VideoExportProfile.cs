namespace KickAutoRecorder.Core.Models;

public class VideoExportProfile
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsFastCopy { get; set; }
    public bool IsAccurateTrim { get; set; }
    public string VideoCodec { get; set; } = "copy";
    public string AudioCodec { get; set; } = "copy";
    public int AudioBitrateKbps { get; set; } = 256;
    public string? Resolution { get; set; }
    public int? Fps { get; set; }
    public bool RequiresNative4KSource { get; set; }

    public static VideoExportProfile FastCopy => new()
    {
        Name = "FastCopy",
        DisplayName = "⚡ Hızlı Kırpma (FAST MODE - Stream Copy)",
        Description = "Yeniden kodlama yapmaz, çok hızlıdır. Kesim noktaları en yakın ana kareye (keyframe) hizalanır.",
        IsFastCopy = true,
        IsAccurateTrim = false,
        VideoCodec = "copy",
        AudioCodec = "copy"
    };

    public static VideoExportProfile Accurate1080p60 => new()
    {
        Name = "Accurate1080p60",
        DisplayName = "🎯 Hassas Kırpma (ACCURATE MODE - 1080p 60fps)",
        Description = "H.264 + AAC kodlama ile kullanıcının seçtiği tam saniyeye milisaniyelik uyum sağlar.",
        IsFastCopy = false,
        IsAccurateTrim = true,
        VideoCodec = "libx264",
        AudioCodec = "aac",
        AudioBitrateKbps = 256,
        Resolution = "1920x1080",
        Fps = 60
    };

    public static VideoExportProfile YouTube4K => new()
    {
        Name = "YouTube4K",
        DisplayName = "🌟 YouTube 4K (ACCURATE MODE - Yalnızca 4K Kaynak)",
        Description = "Yalnızca kaynak video 4K (3840x2160) olduğunda kullanılan hassas kodlama profilidir.",
        IsFastCopy = false,
        IsAccurateTrim = true,
        VideoCodec = "libx264",
        AudioCodec = "aac",
        AudioBitrateKbps = 320,
        Resolution = "3840x2160",
        Fps = 60,
        RequiresNative4KSource = true
    };

    public static VideoExportProfile YouTubeShorts => new()
    {
        Name = "YouTubeShorts",
        DisplayName = "📱 YouTube Shorts (1080x1920 Dikey)",
        Description = "Yatay yayını dikey (9:16) formata çevirir (ortayı kırpar). YouTube Shorts için idealdir.",
        IsFastCopy = false,
        IsAccurateTrim = true,
        VideoCodec = "libx264",
        AudioCodec = "aac",
        AudioBitrateKbps = 256,
        Resolution = "1080x1920",
        Fps = 60,
        RequiresNative4KSource = false
    };
}
