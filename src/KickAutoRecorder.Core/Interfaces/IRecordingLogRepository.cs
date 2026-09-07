using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Entities;

namespace KickAutoRecorder.Core.Interfaces;

public interface IRecordingLogRepository
{
    Task<IEnumerable<RecordingLog>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RecordingLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(RecordingLog log, CancellationToken cancellationToken = default);
    Task UpdateAsync(RecordingLog log, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<RecordingLog>> GetActiveRecordingsAsync(CancellationToken cancellationToken = default);
}
