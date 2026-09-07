using System.Threading;
using System.Threading.Tasks;

namespace KickAutoRecorder.Core.Interfaces;

public interface IThumbnailGeneratorService
{
    /// <summary>
    /// Generates a thumbnail image from a video file at the specified timestamp in seconds.
    /// Supports format ("jpg"/"png"), custom target dimensions (default 1280x720), and aspect ratio handling.
    /// </summary>
    Task<string?> GenerateThumbnailAsync(
        string videoFilePath,
        double timestampSeconds,
        string outputDirectory,
        string format = "jpg",
        int targetWidth = 1280,
        int targetHeight = 720,
        CancellationToken cancellationToken = default);
}
