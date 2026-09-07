using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Infrastructure.Persistence;

namespace KickAutoRecorder.Infrastructure.Repositories;

public class RecordingLogRepository : IRecordingLogRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public RecordingLogRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IEnumerable<RecordingLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.RecordingLogs
            .Include(r => r.Streamer)
            .OrderByDescending(r => r.StartedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<RecordingLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.RecordingLogs
            .Include(r => r.Streamer)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task AddAsync(RecordingLog log, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.RecordingLogs.AddAsync(log, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RecordingLog log, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        var existing = await context.RecordingLogs.FindAsync(new object[] { log.Id }, cancellationToken);
        if (existing != null)
        {
            context.Entry(existing).CurrentValues.SetValues(log);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.RecordingLogs.FindAsync(new object[] { id }, cancellationToken);
        if (item != null)
        {
            context.RecordingLogs.Remove(item);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<RecordingLog>> GetActiveRecordingsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.RecordingLogs
            .Where(r => r.Status == RecordingStatus.Recording || r.Status == RecordingStatus.Pending)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
