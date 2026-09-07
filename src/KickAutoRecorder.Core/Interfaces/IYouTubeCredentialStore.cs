using System.Threading;
using System.Threading.Tasks;

namespace KickAutoRecorder.Core.Interfaces;

public interface IYouTubeCredentialStore
{
    Task SaveTokenAsync(string tokenDataJson, CancellationToken cancellationToken = default);
    Task<string?> LoadTokenAsync(CancellationToken cancellationToken = default);
    Task ClearTokenAsync(CancellationToken cancellationToken = default);
}
