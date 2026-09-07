using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Core.Interfaces;

public interface ISettingsService
{
    RecordingSettings GetSettings();
    Task UpdateSettingsAsync(RecordingSettings settings, CancellationToken cancellationToken = default);
}
