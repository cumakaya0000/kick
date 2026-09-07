namespace KickAutoRecorder.Core.Interfaces;

public interface IFFmpegLocator
{
    string? FindFFmpegExecutable();
    bool IsFFmpegAvailable();
}
