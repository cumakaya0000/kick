using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Infrastructure.Persistence;
using KickAutoRecorder.Infrastructure.Repositories;
using KickAutoRecorder.Infrastructure.Services;
using KickAutoRecorder.Infrastructure.Storage;
using KickAutoRecorder.Recording.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KickAutoRecorder.IntegrationTests;

public class MockFFmpegLocator : IFFmpegLocator
{
    private readonly string? _path;

    public MockFFmpegLocator(string? path = "ffmpeg.exe")
    {
        _path = path;
    }

    public string? FindFFmpegExecutable() => _path;
    public bool IsFFmpegAvailable() => _path != null;
}

public class MockNotificationService : INotificationService
{
    public void ShowNotification(string title, string message, string iconType = "Info") { }
}

public class MockScopeFactory : Microsoft.Extensions.DependencyInjection.IServiceScopeFactory
{
    private readonly IServiceProvider _serviceProvider;
    public MockScopeFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;
    public Microsoft.Extensions.DependencyInjection.IServiceScope CreateScope() => new MockScope(_serviceProvider);

    private class MockScope : Microsoft.Extensions.DependencyInjection.IServiceScope
    {
        public IServiceProvider ServiceProvider { get; }
        public MockScope(IServiceProvider serviceProvider) => ServiceProvider = serviceProvider;
        public void Dispose() { }
    }
}

public class FFmpegRecordingEngineTests
{
    [Fact]
    public void BuildFFmpegArguments_ShouldFormatCorrectParameters()
    {
        var locator = new MockFFmpegLocator();
        var diskService = new DiskService();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source=file:{Guid.NewGuid()}?mode=memory&cache=shared")
            .Options;
        var factory = new TestDbContextFactory(options);
        var repo = new RecordingLogRepository(factory);
        var notificationService = new MockNotificationService();

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<IRecordingLogRepository>(repo);
        var provider = services.BuildServiceProvider();
        var scopeFactory = new MockScopeFactory(provider);
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var settingsService = new SettingsService(config, NullLogger<SettingsService>.Instance);
        var engine = new FFmpegRecordingEngine(locator, diskService, scopeFactory, notificationService, settingsService, NullLogger<FFmpegRecordingEngine>.Instance);

        var streamUrl = "https://stream.kick.com/master.m3u8";
        var outputPath = "C:/Recordings/test.mp4";

        var args = engine.BuildFFmpegArguments(streamUrl, outputPath);

        Assert.Contains("-c", args);
        Assert.Contains("copy", args);
        Assert.Contains("-bsf:a", args);
        Assert.Contains("aac_adtstoasc", args);
        Assert.Contains("-fflags", args);
        Assert.Contains("+genpts+discardcorrupt", args);
        Assert.Contains("-avoid_negative_ts", args);
        Assert.Contains("make_zero", args);
        Assert.Contains("-headers", args);
        Assert.Contains(streamUrl, args);
        Assert.Contains(outputPath, args);
    }

    [Fact]
    public async Task StartRecordingAsync_ShouldThrowFileNotFound_WhenFFmpegMissing()
    {
        var locator = new MockFFmpegLocator(null); // Missing ffmpeg
        var diskService = new DiskService();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source=file:{Guid.NewGuid()}?mode=memory&cache=shared")
            .Options;
        var factory = new TestDbContextFactory(options);
        var repo = new RecordingLogRepository(factory);
        var notificationService = new MockNotificationService();

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<IRecordingLogRepository>(repo);
        var provider = services.BuildServiceProvider();
        var scopeFactory = new MockScopeFactory(provider);
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var settingsService = new SettingsService(config, NullLogger<SettingsService>.Instance);
        var engine = new FFmpegRecordingEngine(locator, diskService, scopeFactory, notificationService, settingsService, NullLogger<FFmpegRecordingEngine>.Instance);

        var streamer = new Streamer
        {
            Id = 1,
            Username = "rraenee",
            CurrentStatus = StreamStatus.Live
        };

        await Assert.ThrowsAsync<FileNotFoundException>(() => 
            engine.StartRecordingAsync(streamer, "https://test.m3u8", AppDomain.CurrentDomain.BaseDirectory));
    }
}
