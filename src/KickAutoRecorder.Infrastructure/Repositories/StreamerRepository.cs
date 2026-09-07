using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Infrastructure.Persistence;

namespace KickAutoRecorder.Infrastructure.Repositories;

public class StreamerRepository : IStreamerRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public StreamerRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IEnumerable<Streamer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Streamers.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<Streamer?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Streamers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Streamer?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Streamers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Username.ToLower() == username.ToLower(), cancellationToken);
    }

    public async Task AddAsync(Streamer streamer, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Streamers.AddAsync(streamer, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Streamer streamer, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        streamer.UpdatedAt = DateTime.Now;
        context.Streamers.Update(streamer);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var streamer = await context.Streamers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (streamer != null)
        {
            context.Streamers.Remove(streamer);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
