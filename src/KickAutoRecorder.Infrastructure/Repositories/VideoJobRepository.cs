using System;
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

public class VideoJobRepository : IVideoJobRepository
{
    private readonly AppDbContext _context;

    public VideoJobRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<VideoJob?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.VideoJobs
            .Include(j => j.RecordingLog)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public async Task<List<VideoJob>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.VideoJobs
            .Include(j => j.RecordingLog)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<VideoJob>> GetPendingOrProcessingJobsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.VideoJobs
            .Where(j => j.Status == VideoJobStatus.Pending || j.Status == VideoJobStatus.Queued || j.Status == VideoJobStatus.Preparing || j.Status == VideoJobStatus.Rendering || j.Status == VideoJobStatus.Validating)
            .OrderBy(j => j.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(VideoJob job, CancellationToken cancellationToken = default)
    {
        if (job == null) throw new ArgumentNullException(nameof(job));
        await _context.VideoJobs.AddAsync(job, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(VideoJob job, CancellationToken cancellationToken = default)
    {
        if (job == null) throw new ArgumentNullException(nameof(job));
        _context.VideoJobs.Update(job);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(int id, VideoJobStatus status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        var job = await _context.VideoJobs.FindAsync(new object[] { id }, cancellationToken);
        if (job != null)
        {
            job.Status = status;
            if (errorMessage != null)
            {
                job.ErrorMessage = errorMessage;
            }
            if ((status == VideoJobStatus.Preparing || status == VideoJobStatus.Rendering) && job.StartedAt == null)
            {
                job.StartedAt = DateTime.UtcNow;
            }
            else if (status == VideoJobStatus.Completed || status == VideoJobStatus.Failed || status == VideoJobStatus.Cancelled || status == VideoJobStatus.Corrupted || status == VideoJobStatus.Interrupted)
            {
                job.CompletedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var job = await _context.VideoJobs.FindAsync(new object[] { id }, cancellationToken);
        if (job != null)
        {
            _context.VideoJobs.Remove(job);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
