using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Entities;

namespace KickAutoRecorder.Core.Interfaces;

public interface IStreamerRepository
{
    Task<IEnumerable<Streamer>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Streamer?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Streamer?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task AddAsync(Streamer streamer, CancellationToken cancellationToken = default);
    Task UpdateAsync(Streamer streamer, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
