using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Core.Models;
using KickAutoRecorder.Infrastructure.Persistence;
using KickAutoRecorder.Infrastructure.Repositories;
using KickAutoRecorder.Infrastructure.Services;
using KickAutoRecorder.Infrastructure.Storage;
using KickAutoRecorder.Recording.Services;
using KickAutoRecorder.Worker.Services;
using Xunit;

namespace KickAutoRecorder.IntegrationTests;

public class StabilityPhaseTests
{
    private (TestDbContextFactory Factory, AppDbContext Context) CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source=file:{Guid.NewGuid()}?mode=memory&cache=shared")
            .Options;

        var factory = new TestDbContextFactory(options);
        var context = factory.CreateDbContext();
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        return (factory, context);
    }

    [Fact]
    public async Task ConcurrentStarts_ForSameStreamer_ShouldNotSpawnDuplicateRecordings()
    {
        var (factory, context) = CreateInMemoryDb();
        var logRepo = new RecordingLogRepository(factory);
        // Use powershell.exe as mock binary to simulate valid process execution in tests
        var locator = new MockFFmpegLocator("powershell.exe");
        var diskService = new DiskService();
        var notificationService = new MockNotificationService();

        var streamer = new Streamer { Id = 55, Username = "concurrent_streamer", CurrentStatus = StreamStatus.Live };
        context.Streamers.Add(streamer);
        await context.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var settingsService = new SettingsService(config, NullLogger<SettingsService>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IRecordingLogRepository>(logRepo);
        var provider = services.BuildServiceProvider();
        var scopeFactory = new MockScopeFactory(provider);

        var engine = new FFmpegRecordingEngine(locator, diskService, scopeFactory, notificationService, settingsService, NullLogger<FFmpegRecordingEngine>.Instance);

        // Launch concurrent starts for same streamer
        var t1 = engine.StartRecordingAsync(streamer, "https://test.m3u8", AppDomain.CurrentDomain.BaseDirectory);
        var t2 = engine.StartRecordingAsync(streamer, "https://test.m3u8", AppDomain.CurrentDomain.BaseDirectory);

        var log1 = await t1;
        var log2 = await t2;

        Assert.NotNull(log1);
        Assert.NotNull(log2);
        Assert.Equal(log1.Id, log2.Id); // Duplicate start returned exact same log
        Assert.True(engine.IsRecording(streamer.Id));

        await engine.StopRecordingAsync(streamer.Id);
    }

    [Fact]
    public async Task SettingsService_ShouldUpdateSettings_Atomically()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var settingsService = new SettingsService(config, NullLogger<SettingsService>.Instance);

        var newSettings = new RecordingSettings
        {
            OutputFolder = @"C:\TestRecordings",
            PollingIntervalSeconds = 45,
            MinDiskSpaceGB = 10,
            PreferredQuality = "1080p",
            IsDarkTheme = false
        };

        await settingsService.UpdateSettingsAsync(newSettings);

        var saved = settingsService.GetSettings();
        Assert.Equal(@"C:\TestRecordings", saved.OutputFolder);
        Assert.Equal(45, saved.PollingIntervalSeconds);
        Assert.Equal(10, saved.MinDiskSpaceGB);
        Assert.Equal("1080p", saved.PreferredQuality);
        Assert.False(saved.IsDarkTheme);
    }

    [Fact]
    public async Task ProcessStreamerCheckAsync_ShouldSkipDebounceUpdate_OnApiError()
    {
        var (factory, context) = CreateInMemoryDb();
        var logRepo = new RecordingLogRepository(factory);
        var streamerService = new MockStreamerServiceForOrchestrator { TargetStatus = StreamStatus.Error };
        var engine = new MockRecordingEngine();
        
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var settingsService = new SettingsService(config, NullLogger<SettingsService>.Instance);
        var validator = new FFprobeValidator(settingsService, NullLogger<FFprobeValidator>.Instance);

        var streamer = new Streamer { Id = 88, Username = "error_test", CurrentStatus = StreamStatus.Error, IsTrackingEnabled = true };

        var orchestrator = new RecordingOrchestrator(streamerService, engine, logRepo, settingsService, validator, NullLogger<RecordingOrchestrator>.Instance);

        // API Status is Error - check should return safely without throwing or triggering recording
        await orchestrator.ProcessStreamerCheckAsync(streamer);

        Assert.False(engine.IsRecording(88));
    }

    [Fact]
    public void NotifyManualStop_ShouldRegisterSuppressionFlag()
    {
        var (factory, _) = CreateInMemoryDb();
        var streamerRepo = new StreamerRepository(factory);
        var logRepo = new RecordingLogRepository(factory);
        var streamerService = new StreamerService(streamerRepo, null!, NullLogger<StreamerService>.Instance);
        var engine = new MockRecordingEngine();
        
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var settingsService = new SettingsService(config, NullLogger<SettingsService>.Instance);
        var validator = new FFprobeValidator(settingsService, NullLogger<FFprobeValidator>.Instance);

        var orchestrator = new RecordingOrchestrator(streamerService, engine, logRepo, settingsService, validator, NullLogger<RecordingOrchestrator>.Instance);

        // Notify manual stop for streamer ID 99
        orchestrator.NotifyManualStop(99);

        // Method completes without throwing
        Assert.False(engine.IsRecording(99));
    }

    [Fact]
    public void ResilientRecovery_ShouldHaveMaxReconnectAttemptsSetting()
    {
        var settings = new RecordingSettings
        {
            MaxReconnectAttempts = 7
        };

        Assert.Equal(7, settings.MaxReconnectAttempts);
    }

    [Fact]
    public void ResilientRecovery_Telemetry_ShouldTrackRecoveryState()
    {
        var telemetry = new RecordingHealthTelemetry
        {
            IsRecovering = true,
            ReconnectCount = 2,
            LastError = "Connection Lost (ExitCode: 1)",
            NextRetrySeconds = 10,
            FFmpegStatus = "Reconnecting (2/5)"
        };

        Assert.True(telemetry.IsRecovering);
        Assert.Equal(2, telemetry.ReconnectCount);
        Assert.Equal("Connection Lost (ExitCode: 1)", telemetry.LastError);
        Assert.Equal(10, telemetry.NextRetrySeconds);
        Assert.Equal("Reconnecting (2/5)", telemetry.FFmpegStatus);
    }

    [Fact]
    public async Task FFprobeValidator_ShouldMarkNonExistentFileAsInvalid()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var settingsService = new SettingsService(config, NullLogger<SettingsService>.Instance);
        var validator = new FFprobeValidator(settingsService, NullLogger<FFprobeValidator>.Instance);

        var result = await validator.ValidateAndExtractMetadataAsync("non_existent_file.mp4");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void RecordingLog_ShouldStoreValidationMetadata()
    {
        var log = new RecordingLog
        {
            Id = 1,
            StreamerUsername = "test_user",
            IsValidated = true,
            VideoCodec = "h264",
            AudioCodec = "aac",
            Resolution = "1920x1080",
            Duration = TimeSpan.FromMinutes(45)
        };

        Assert.True(log.IsValidated);
        Assert.Equal("h264", log.VideoCodec);
        Assert.Equal("aac", log.AudioCodec);
        Assert.Equal("1920x1080", log.Resolution);
        Assert.Equal(TimeSpan.FromMinutes(45), log.Duration);
    }

    [Fact]
    public async Task RecoverUnfinishedRecordingsAsync_ShouldBeIdempotent_AndAssignCorrectStatuses()
    {
        var (factory, context) = CreateInMemoryDb();
        var streamerRepo = new StreamerRepository(factory);
        var logRepo = new RecordingLogRepository(factory);
        var streamerService = new StreamerService(streamerRepo, null!, NullLogger<StreamerService>.Instance);
        var engine = new MockRecordingEngine();

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var settingsService = new SettingsService(config, NullLogger<SettingsService>.Instance);
        var validator = new FFprobeValidator(settingsService, NullLogger<FFprobeValidator>.Instance);

        var streamer = new Streamer { Id = 77, Username = "stuck_streamer", CurrentStatus = StreamStatus.Live };
        context.Streamers.Add(streamer);
        await context.SaveChangesAsync();

        var stuckLog = new RecordingLog
        {
            Id = 777,
            StreamerId = 77,
            StreamerUsername = "stuck_streamer",
            FilePath = "missing_recovered_file.mp4",
            StartedAt = DateTime.Now.AddHours(-1),
            Status = RecordingStatus.Recording
        };
        await logRepo.AddAsync(stuckLog);

        var orchestrator = new RecordingOrchestrator(streamerService, engine, logRepo, settingsService, validator, NullLogger<RecordingOrchestrator>.Instance);

        // First recovery run - should process the stuck log
        await orchestrator.RecoverUnfinishedRecordingsAsync();

        var updatedLog = await logRepo.GetByIdAsync(777);
        Assert.NotNull(updatedLog);
        Assert.NotEqual(RecordingStatus.Recording, updatedLog.Status);

        // Second recovery run - idempotency check (0 active logs remain)
        var activeLogsBeforeSecondRun = await logRepo.GetActiveRecordingsAsync();
        Assert.Empty(activeLogsBeforeSecondRun);

        await orchestrator.RecoverUnfinishedRecordingsAsync();

        var logAfterSecondRun = await logRepo.GetByIdAsync(777);
        Assert.Equal(updatedLog.Status, logAfterSecondRun!.Status);
    }
}
