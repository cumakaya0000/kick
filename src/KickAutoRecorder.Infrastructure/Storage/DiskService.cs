using System;
using System.IO;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Infrastructure.Storage;

public class DiskService : IDiskService
{
    public long GetAvailableFreeSpaceBytes(string directoryPath)
    {
        try
        {
            EnsureDirectoryExists(directoryPath);
            var root = Path.GetPathRoot(Path.GetFullPath(directoryPath));
            if (string.IsNullOrEmpty(root))
                return 0;

            var drive = new DriveInfo(root);
            return drive.AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }

    public bool HasSufficientSpace(string directoryPath, long requiredBytes)
    {
        var freeBytes = GetAvailableFreeSpaceBytes(directoryPath);
        return freeBytes >= requiredBytes;
    }

    public void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }
}
