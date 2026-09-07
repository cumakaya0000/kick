using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Core.Interfaces;

public interface IFFprobeValidator
{
    Task<FFprobeMetadata> ValidateAndExtractMetadataAsync(string filePath, CancellationToken cancellationToken = default);
    string? FindFFprobeExecutable();
}
