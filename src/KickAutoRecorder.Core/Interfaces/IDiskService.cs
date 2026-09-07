namespace KickAutoRecorder.Core.Interfaces;

public interface IDiskService
{
    long GetAvailableFreeSpaceBytes(string directoryPath);
    bool HasSufficientSpace(string directoryPath, long requiredBytes);
    void EnsureDirectoryExists(string directoryPath);
}
