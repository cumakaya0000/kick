using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Infrastructure.Persistence;
using KickAutoRecorder.Infrastructure.Repositories;
using Xunit;

namespace KickAutoRecorder.IntegrationTests;

public class YouTubeMetadataDraftTests
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
    public async Task SaveOrUpdateDraft_ShouldCreateNewDraft_WhenNotExists()
    {
        var (context, connection) = CreateInMemoryDbContext();
        using (connection)
        using (context)
        {
            var log = await CreateTestRecordingLogAsync(context, "creator1");
            var repo = new YouTubeMetadataDraftRepository(context);

            var draft = new YouTubeVideoDraft
            {
                RecordingLogId = log.Id,
                StreamerUsername = log.StreamerUsername,
                Title = "Best Plays of the Week",
                Description = "Awesome highlights description",
                Tags = "kick, gaming, highlights",
                Category = "Gaming",
                Language = "Turkish",
                PrivacyStatus = "Unlisted",
                MadeForKids = false
            };

            await repo.SaveOrUpdateAsync(draft);

            var retrieved = await repo.GetByRecordingLogIdAsync(log.Id);
            Assert.NotNull(retrieved);
            Assert.Equal("Best Plays of the Week", retrieved.Title);
            Assert.Equal("Unlisted", retrieved.PrivacyStatus);
        }
    }

    [Fact]
    public async Task SaveOrUpdateDraft_ShouldUpdateExistingDraft_WhenExists()
    {
        var (context, connection) = CreateInMemoryDbContext();
        using (connection)
        using (context)
        {
            var log = await CreateTestRecordingLogAsync(context, "creator2");
            var repo = new YouTubeMetadataDraftRepository(context);

            var draft1 = new YouTubeVideoDraft
            {
                RecordingLogId = log.Id,
                StreamerUsername = log.StreamerUsername,
                Title = "Initial Title",
                Description = "Initial Description",
                PrivacyStatus = "Private"
            };
            await repo.SaveOrUpdateAsync(draft1);

            var draft2 = new YouTubeVideoDraft
            {
                RecordingLogId = log.Id,
                StreamerUsername = log.StreamerUsername,
                Title = "Updated Title",
                Description = "Updated Description",
                PrivacyStatus = "Public"
            };
            await repo.SaveOrUpdateAsync(draft2);

            var retrieved = await repo.GetByRecordingLogIdAsync(log.Id);
            Assert.NotNull(retrieved);
            Assert.Equal("Updated Title", retrieved.Title);
            Assert.Equal("Public", retrieved.PrivacyStatus);
            Assert.NotNull(retrieved.UpdatedAt);
        }
    }

    [Fact]
    public void DraftJsonExport_ShouldFormatValidJson()
    {
        var draftData = new
        {
            Title = "Sample YouTube Title",
            Description = "Sample Description",
            Tags = "tag1, tag2",
            Category = "Gaming",
            Language = "Turkish",
            PrivacyStatus = "Private",
            MadeForKids = false
        };

        var json = JsonSerializer.Serialize(draftData, new JsonSerializerOptions { WriteIndented = true });
        Assert.Contains("Sample YouTube Title", json);
        Assert.Contains("Turkish", json);
    }
}
