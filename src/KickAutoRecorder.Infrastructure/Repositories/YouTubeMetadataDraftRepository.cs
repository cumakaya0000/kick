using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Infrastructure.Persistence;

namespace KickAutoRecorder.Infrastructure.Repositories;

public class YouTubeMetadataDraftRepository : IYouTubeMetadataDraftRepository
{
    private readonly AppDbContext _context;

    public YouTubeMetadataDraftRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<YouTubeVideoDraft?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.YouTubeVideoDrafts
            .Include(d => d.RecordingLog)
            .Include(d => d.VideoJob)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<YouTubeVideoDraft?> GetByRecordingLogIdAsync(int recordingLogId, CancellationToken cancellationToken = default)
    {
        return await _context.YouTubeVideoDrafts
            .Include(d => d.RecordingLog)
            .Include(d => d.VideoJob)
            .FirstOrDefaultAsync(d => d.RecordingLogId == recordingLogId, cancellationToken);
    }

    public async Task<List<YouTubeVideoDraft>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.YouTubeVideoDrafts
            .Include(d => d.RecordingLog)
            .Include(d => d.VideoJob)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(YouTubeVideoDraft draft, CancellationToken cancellationToken = default)
    {
        if (draft == null) throw new ArgumentNullException(nameof(draft));
        await _context.YouTubeVideoDrafts.AddAsync(draft, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(YouTubeVideoDraft draft, CancellationToken cancellationToken = default)
    {
        if (draft == null) throw new ArgumentNullException(nameof(draft));
        draft.UpdatedAt = DateTime.UtcNow;
        _context.YouTubeVideoDrafts.Update(draft);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveOrUpdateAsync(YouTubeVideoDraft draft, CancellationToken cancellationToken = default)
    {
        if (draft == null) throw new ArgumentNullException(nameof(draft));

        var existing = await _context.YouTubeVideoDrafts
            .FirstOrDefaultAsync(d => d.RecordingLogId == draft.RecordingLogId, cancellationToken);

        if (existing != null)
        {
            existing.Title = draft.Title;
            existing.Description = draft.Description;
            existing.Tags = draft.Tags;
            existing.Category = draft.Category;
            existing.Language = draft.Language;
            existing.GameName = draft.GameName;
            existing.ThumbnailPath = draft.ThumbnailPath;
            existing.PrivacyStatus = draft.PrivacyStatus;
            existing.PlaylistName = draft.PlaylistName;
            existing.MadeForKids = draft.MadeForKids;
            existing.VideoJobId = draft.VideoJobId ?? existing.VideoJobId;
            existing.UpdatedAt = DateTime.UtcNow;

            _context.YouTubeVideoDrafts.Update(existing);
        }
        else
        {
            await _context.YouTubeVideoDrafts.AddAsync(draft, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var draft = await _context.YouTubeVideoDrafts.FindAsync(new object[] { id }, cancellationToken);
        if (draft != null)
        {
            _context.YouTubeVideoDrafts.Remove(draft);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
