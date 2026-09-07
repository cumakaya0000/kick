using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Infrastructure.Kick;
using KickAutoRecorder.Infrastructure.Persistence;
using KickAutoRecorder.Infrastructure.Repositories;
using KickAutoRecorder.Infrastructure.Services;
using Xunit;

namespace KickAutoRecorder.IntegrationTests;

public class MockKickApiClient : IKickApiClient
{
    public Task<KickChannelStatusResult> CheckChannelStatusAsync(string username, System.Threading.CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new KickChannelStatusResult(
            Username: username,
            Status: StreamStatus.Live,
            StreamTitle: "Test Live Broadcast",
            PlaybackUrl: "https://stream.kick.com/test.m3u8",
            ViewerCount: 1200,
            Category: "Just Chatting"
        ));
    }
}

public class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;
    public TestDbContextFactory(DbContextOptions<AppDbContext> options) => _options = options;
    public AppDbContext CreateDbContext() => new AppDbContext(_options);
    public Task<AppDbContext> CreateDbContextAsync(System.Threading.CancellationToken cancellationToken = default) 
        => Task.FromResult(new AppDbContext(_options));
}

public class StreamerServiceTests
{
    private (AppDbContext Context, StreamerService Service) CreateInMemoryService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source=file:{Guid.NewGuid()}?mode=memory&cache=shared")
            .Options;

        var context = new AppDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        var factory = new TestDbContextFactory(options);
        var repo = new StreamerRepository(factory);
        var mockKickApi = new MockKickApiClient();
        var logger = NullLogger<StreamerService>.Instance;
        var service = new StreamerService(repo, mockKickApi, logger);

        return (context, service);
    }

    [Theory]
    [InlineData("rraenee", "rraenee", "https://kick.com/rraenee")]
    [InlineData("@rraenee", "rraenee", "https://kick.com/rraenee")]
    [InlineData("https://kick.com/rraenee", "rraenee", "https://kick.com/rraenee")]
    [InlineData("HTTP://KICK.COM/RRAENEE", "rraenee", "https://kick.com/rraenee")]
    public void NormalizeStreamerInput_ShouldNormalizeCorrectly(string input, string expectedUsername, string expectedUrl)
    {
        var (_, service) = CreateInMemoryService();
        var (user, url) = service.NormalizeStreamerInput(input);

        Assert.Equal(expectedUsername, user);
        Assert.Equal(expectedUrl, url);
    }

    [Fact]
    public async Task AddStreamerAsync_ShouldAddValidStreamer()
    {
        var (_, service) = CreateInMemoryService();

        var result = await service.AddStreamerAsync("rraenee");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Streamer);
        Assert.Equal("rraenee", result.Streamer.Username);
        Assert.Equal("https://kick.com/rraenee", result.Streamer.ChannelUrl);
        Assert.True(result.Streamer.IsTrackingEnabled);
    }

    [Fact]
    public async Task AddStreamerAsync_ShouldRejectDuplicateStreamer()
    {
        var (_, service) = CreateInMemoryService();

        await service.AddStreamerAsync("rraenee");
        var duplicateResult = await service.AddStreamerAsync("https://kick.com/RRAENEE");

        Assert.False(duplicateResult.IsSuccess);
        Assert.Contains("zaten listede ekli", duplicateResult.ErrorMessage);
    }

    [Fact]
    public async Task AddStreamerAsync_ShouldRejectEmptyInput()
    {
        var (_, service) = CreateInMemoryService();

        var result = await service.AddStreamerAsync("   ");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task CheckAndUpdateStreamerStatusAsync_ShouldUpdateLivestreamDetails()
    {
        var (_, service) = CreateInMemoryService();
        var addResult = await service.AddStreamerAsync("rraenee");
        var streamerId = addResult.Streamer!.Id;

        var updated = await service.CheckAndUpdateStreamerStatusAsync(streamerId);

        Assert.NotNull(updated);
        Assert.Equal(StreamStatus.Live, updated.CurrentStatus);
        Assert.Equal("Test Live Broadcast", updated.StreamTitle);
        Assert.Equal("Just Chatting", updated.Category);
        Assert.Equal(1200, updated.ViewerCount);
        Assert.Equal("https://stream.kick.com/test.m3u8", updated.PlaybackUrl);
    }

    [Fact]
    public async Task ToggleTrackingAsync_ShouldInvertStatus()
    {
        var (_, service) = CreateInMemoryService();

        var addResult = await service.AddStreamerAsync("rraenee");
        var streamerId = addResult.Streamer!.Id;

        var toggled = await service.ToggleTrackingAsync(streamerId);
        Assert.True(toggled);

        var list = await service.GetAllStreamersAsync();
        var streamer = Assert.Single(list);
        Assert.False(streamer.IsTrackingEnabled);
    }

    [Fact]
    public async Task DeleteStreamerAsync_ShouldRemoveStreamer()
    {
        var (_, service) = CreateInMemoryService();

        var addResult = await service.AddStreamerAsync("rraenee");
        var streamerId = addResult.Streamer!.Id;

        var deleted = await service.DeleteStreamerAsync(streamerId);
        Assert.True(deleted);

        var list = await service.GetAllStreamersAsync();
        Assert.Empty(list);
    }
}
