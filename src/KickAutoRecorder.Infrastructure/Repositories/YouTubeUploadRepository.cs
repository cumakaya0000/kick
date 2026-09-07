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

public class YouTubeUploadRepository : IYouTubeUploadRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public YouTubeUploadRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IEnumerable<YouTubeUploadJob>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<YouTubeUploadJob>()
            .Include(j => j.RecordingLog)
            .Include(j => j.RenderJob)
            .OrderByDescending(j => j.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<YouTubeUploadJob?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<YouTubeUploadJob>()
            .Include(j => j.RecordingLog)
            .Include(j => j.RenderJob)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public async Task<YouTubeUploadJob?> GetByRenderJobIdAsync(int renderJobId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<YouTubeUploadJob>()
            .Include(j => j.RecordingLog)
            .Include(j => j.RenderJob)
            .FirstOrDefaultAsync(j => j.RenderJobId == renderJobId, cancellationToken);
    }

    public async Task<IEnumerable<YouTubeUploadJob>> GetPendingOrUploadingJobsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<YouTubeUploadJob>()
            .Where(j => j.Status == YouTubeUploadStatus.Pending ||
                        j.Status == YouTubeUploadStatus.Uploading ||
                        j.Status == YouTubeUploadStatus.Processing ||
                        j.Status == YouTubeUploadStatus.WaitingForRender ||
                        j.Status == YouTubeUploadStatus.RetryScheduled)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(YouTubeUploadJob job, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Set<YouTubeUploadJob>().AddAsync(job, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(YouTubeUploadJob job, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Set<YouTubeUploadJob>().Update(job);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(int id, YouTubeUploadStatus status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.Set<YouTubeUploadJob>().FindAsync(new object[] { id }, cancellationToken);
        if (item != null)
        {
            item.Status = status;
            if (errorMessage != null)
            {
                item.LastError = errorMessage;
            }
            if (status == YouTubeUploadStatus.Completed)
            {
                item.CompletedAt = DateTime.UtcNow;
            }
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateProgressAsync(int id, double progressPercent, long uploadedBytes, long totalBytes, string? sessionUri = null, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.Set<YouTubeUploadJob>().FindAsync(new object[] { id }, cancellationToken);
        if (item != null)
        {
            item.ProgressPercent = progressPercent;
            item.UploadedBytes = uploadedBytes;
            item.TotalBytes = totalBytes;
            if (sessionUri != null)
            {
                item.UploadSessionUri = sessionUri;
            }
            item.LastProgressAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.Set<YouTubeUploadJob>().FindAsync(new object[] { id }, cancellationToken);
        if (item != null)
        {
            context.Set<YouTubeUploadJob>().Remove(item);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
