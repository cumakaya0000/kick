using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Infrastructure.Services;

public class FFmpegThumbnailGenerator : IThumbnailGeneratorService
{
    private readonly IFFmpegLocator _ffmpegLocator;
    private readonly IDiskService _diskService;
    private readonly ILogger<FFmpegThumbnailGenerator> _logger;

    public FFmpegThumbnailGenerator(
        IFFmpegLocator ffmpegLocator,
        IDiskService diskService,
        ILogger<FFmpegThumbnailGenerator> logger)
    {
        _ffmpegLocator = ffmpegLocator;
        _diskService = diskService;
        _logger = logger;
    }

    public async Task<string?> GenerateThumbnailAsync(
        string videoFilePath, 
        double timestampSeconds, 
        string outputDirectory, 
        string format = "jpg",
        int targetWidth = 1280,
        int targetHeight = 720,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoFilePath) || !File.Exists(videoFilePath))
        {
            _logger.LogWarning("Cannot generate thumbnail. Source video file does not exist: {Path}", videoFilePath);
            return null;
        }

        var ffmpegExe = _ffmpegLocator.FindFFmpegExecutable();
        if (string.IsNullOrEmpty(ffmpegExe))
        {
            _logger.LogError("FFmpeg executable not found. Cannot generate thumbnail.");
            return null;
        }

        _diskService.EnsureDirectoryExists(outputDirectory);

        var ext = string.Equals(format, "png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(videoFilePath);
        var timeTag = ((long)timestampSeconds).ToString();
        var outputPath = Path.Combine(outputDirectory, $"{fileNameWithoutExt}_thumb_{timeTag}.{ext}");

        var formattedTime = TimeSpan.FromSeconds(timestampSeconds).ToString(@"hh\:mm\:ss\.fff");
        
        // Scale to target dimensions while maintaining aspect ratio and letterboxing if needed
        var vfFilter = $"scale={targetWidth}:{targetHeight}:force_original_aspect_ratio=decrease,pad={targetWidth}:{targetHeight}:(ow-iw)/2:(oh-ih)/2";
        var formatArg = ext == "jpg" ? "-q:v 2" : "";

        var arguments = $"-y -ss {formattedTime} -i \"{videoFilePath}\" -vframes 1 -vf \"{vfFilter}\" {formatArg} \"{outputPath}\"";

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
            {
                _logger.LogInformation("Successfully generated 1280x720 thumbnail at {OutputPath}", outputPath);
                return outputPath;
            }
            else
            {
                _logger.LogWarning("FFmpeg exited with code {Code} while generating thumbnail.", process.ExitCode);
                if (File.Exists(outputPath)) File.Delete(outputPath);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception generating thumbnail for {VideoPath}", videoFilePath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
            return null;
        }
    }
}
