using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Models;
using KickAutoRecorder.Infrastructure.Persistence;
using KickAutoRecorder.Infrastructure.Repositories;
using Xunit;

namespace KickAutoRecorder.IntegrationTests;

public class VideoPipelineTests
{
    private (AppDbContext context, SqliteConnection connection) CreateInMemoryDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return (context, connection);
    }

    private async Task<RecordingLog> CreateTestRecordingLogAsync(AppDbContext context, string username = "teststreamer")
    {
        var streamer = new Streamer
        {
            Username = username,
            ChannelUrl = $"https://kick.com/{username}"
        };
        await context.Streamers.AddAsync(streamer);
        await context.SaveChangesAsync();

        var log = new RecordingLog
        {
            StreamerId = streamer.Id,
            StreamerUsername = streamer.Username,
            StreamTitle = "Test Stream Title",
            FilePath = "C:\\test\\segment_001.mp4",
            Status = RecordingStatus.Completed
        };
        await context.RecordingLogs.AddAsync(log);
        await context.SaveChangesAsync();

        return log;
    }

    [Fact]
    public async Task VideoJobRepository_AddAndRetrieveJob_ShouldSucceed()
    {
        var (context, connection) = CreateInMemoryDbContext();
        using (connection)
        using (context)
        {
            var log = await CreateTestRecordingLogAsync(context, "teststreamer");
            var repo = new VideoJobRepository(context);

            var job = new VideoJob
            {
                RecordingLogId = log.Id,
                StreamerUsername = log.StreamerUsername,
                Title = "Test Stream Highlight",
                Description = "Highlight description",
                ExportPreset = "Accurate1080p60",
                OutputPath = "C:\\test\\output.mp4",
                Status = VideoJobStatus.Queued
            };

            await repo.AddAsync(job);

            var retrieved = await repo.GetByIdAsync(job.Id);
            Assert.NotNull(retrieved);
            Assert.Equal("teststreamer", retrieved.StreamerUsername);
            Assert.Equal(VideoJobStatus.Queued, retrieved.Status);
        }
    }

    [Fact]
    public async Task VideoJobRepository_UpdateStatus_ShouldUpdateStatusAndCompletedAt()
    {
        var (context, connection) = CreateInMemoryDbContext();
        using (connection)
        using (context)
        {
            var log = await CreateTestRecordingLogAsync(context, "gamer1");
            var repo = new VideoJobRepository(context);

            var job = new VideoJob
            {
                RecordingLogId = log.Id,
                StreamerUsername = log.StreamerUsername,
                Title = "Game Clip",
                OutputPath = "C:\\test\\clip.mp4",
                Status = VideoJobStatus.Rendering,
                StartedAt = DateTime.UtcNow
            };

            await repo.AddAsync(job);
            await repo.UpdateStatusAsync(job.Id, VideoJobStatus.Completed);

            var updated = await repo.GetByIdAsync(job.Id);
            Assert.NotNull(updated);
            Assert.Equal(VideoJobStatus.Completed, updated.Status);
            Assert.NotNull(updated.CompletedAt);
        }
    }

    [Fact]
    public void VideoExportProfile_Presets_ShouldMatchRequirements()
    {
        var fastCopy = VideoExportProfile.FastCopy;
        Assert.True(fastCopy.IsFastCopy);
        Assert.Equal("copy", fastCopy.VideoCodec);

        var yt1080p = VideoExportProfile.Accurate1080p60;
        Assert.False(yt1080p.IsFastCopy);
        Assert.True(yt1080p.IsAccurateTrim);
        Assert.Equal("libx264", yt1080p.VideoCodec);
        Assert.Equal("aac", yt1080p.AudioCodec);
        Assert.Equal(256, yt1080p.AudioBitrateKbps);

        var yt4k = VideoExportProfile.YouTube4K;
        Assert.True(yt4k.RequiresNative4KSource);
    }
}
