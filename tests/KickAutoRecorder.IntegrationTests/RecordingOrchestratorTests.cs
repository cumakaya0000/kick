using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Core.Models;
using KickAutoRecorder.Infrastructure.Persistence;
using KickAutoRecorder.Infrastructure.Repositories;
using KickAutoRecorder.Infrastructure.Services;
using KickAutoRecorder.Worker.Services;
using Xunit;

namespace KickAutoRecorder.IntegrationTests;

public class MockFFprobeValidator : IFFprobeValidator
{
    public Task<FFprobeMetadata> ValidateAndExtractMetadataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new FFprobeMetadata
        {
            IsValid = File.Exists(filePath),
            FileSizeBytes = File.Exists(filePath) ? new FileInfo(filePath).Length : 0
        });
    }

    public string? FindFFprobeExecutable() => null;
}

public class MockRecordingEngine : IRecordingEngine
{
    public bool StartRecordingCalled { get; private set; }
    public bool StopRecordingCalled { get; private set; }
    public bool IsRecordingActive { get; set; }

    public event Action<RecordingHealthTelemetry>? OnTelemetryUpdated;

    public Task<RecordingLog> StartRecordingAsync(Streamer streamer, string streamUrl, string outputDirectory, CancellationToken cancellationToken = default)
    {
        StartRecordingCalled = true;
        IsRecordingActive = true;
        return Task.FromResult(new RecordingLog
        {
            Id = 99,
            StreamerId = streamer.Id,
            StreamerUsername = streamer.Username,
            FilePath = "test.mp4",
            Status = RecordingStatus.Recording
        });
    }

    public Task StopRecordingAsync(int recordingLogId, CancellationToken cancellationToken = default)
    {
        StopRecordingCalled = true;
        IsRecordingActive = false;
        return Task.CompletedTask;
    }

    public bool IsRecording(int streamerId) => IsRecordingActive;
    public RecordingHealthTelemetry? GetActiveTelemetry(int streamerId) => null;
}

public class MockStreamerServiceForOrchestrator : IStreamerService
{
    public StreamStatus TargetStatus { get; set; } = StreamStatus.Live;

    public Task<IEnumerable<Streamer>> GetAllStreamersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Streamer>>(new List<Streamer>());

    public Task<AddStreamerResult> AddStreamerAsync(string usernameOrUrl, CancellationToken cancellationToken = default)
        => Task.FromResult(new AddStreamerResult(true, null, null));

    public Task<bool> ToggleTrackingAsync(int streamerId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> DeleteStreamerAsync(int streamerId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<Streamer?> CheckAndUpdateStreamerStatusAsync(int streamerId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Streamer?>(new Streamer
        {
            Id = streamerId,
            Username = "rraenee",
            CurrentStatus = TargetStatus,
            PlaybackUrl = "https://stream.kick.com/test.m3u8",
            IsTrackingEnabled = true
        });
    }

    public Task<bool> UpdatePreferredQualityAsync(int streamerId, string quality, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task CheckAndUpdateAllStatusesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public (string NormalizedUsername, string ChannelUrl) NormalizeStreamerInput(string input) => (input, input);
}

public class RecordingOrchestratorTests
{
    private (AppDbContext Context, StreamerRepository StreamerRepo, RecordingLogRepository LogRepo) CreateInMemoryRepos()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source=file:{Guid.NewGuid()}?mode=memory&cache=shared")
            .Options;

        var factory = new TestDbContextFactory(options);
        var context = factory.CreateDbContext();
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return (context, new StreamerRepository(factory), new RecordingLogRepository(factory));
    }

    [Fact]
    public async Task ProcessStreamerCheckAsync_ShouldStartRecording_WhenStreamerIsLiveTwiceDebounced()
    {
        var (_, streamerRepo, logRepo) = CreateInMemoryRepos();
        var streamer = new Streamer { Username = "rraenee", IsTrackingEnabled = true };
        await streamerRepo.AddAsync(streamer);

        var streamerService = new MockStreamerServiceForOrchestrator { TargetStatus = StreamStatus.Live };
        var mockEngine = new MockRecordingEngine { IsRecordingActive = false };
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var settingsService = new SettingsService(config, NullLogger<SettingsService>.Instance);
        var ffprobeValidator = new MockFFprobeValidator();

        var orchestrator = new RecordingOrchestrator(
            streamerService, 
            mockEngine, 
            logRepo, 
            settingsService,
            ffprobeValidator,
            NullLogger<RecordingOrchestrator>.Instance);

        // 1st Live check (debouncing, requires 2)
        await orchestrator.ProcessStreamerCheckAsync(streamer);
        Assert.False(mockEngine.StartRecordingCalled);

        // 2nd Live check (confirmed)
        await orchestrator.ProcessStreamerCheckAsync(streamer);
        Assert.True(mockEngine.StartRecordingCalled);
    }

    [Fact]
    public async Task ProcessStreamerCheckAsync_ShouldStopRecording_WhenStreamerGoesOfflineThriceDebounced()
    {
        var (_, streamerRepo, logRepo) = CreateInMemoryRepos();

        var streamer = new Streamer { Username = "rraenee", IsTrackingEnabled = true };
        await streamerRepo.AddAsync(streamer);

        var log = new RecordingLog
        {
            StreamerId = streamer.Id,
            StreamerUsername = streamer.Username,
            FilePath = "test.mp4",
            Status = RecordingStatus.Recording
        };
        await logRepo.AddAsync(log);

        var streamerService = new MockStreamerServiceForOrchestrator { TargetStatus = StreamStatus.Offline };
        var mockEngine = new MockRecordingEngine { IsRecordingActive = true };
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var settingsService = new SettingsService(config, NullLogger<SettingsService>.Instance);
        var ffprobeValidator = new MockFFprobeValidator();

        var orchestrator = new RecordingOrchestrator(
            streamerService, 
            mockEngine, 
            logRepo, 
            settingsService, 
            ffprobeValidator,
            NullLogger<RecordingOrchestrator>.Instance);

        // 1st & 2nd Offline check (debouncing, requires 3)
        await orchestrator.ProcessStreamerCheckAsync(streamer);
        await orchestrator.ProcessStreamerCheckAsync(streamer);
        Assert.False(mockEngine.StopRecordingCalled);

        // 3rd Offline check (confirmed)
        await orchestrator.ProcessStreamerCheckAsync(streamer);
        Assert.True(mockEngine.StopRecordingCalled);
    }

    [Fact]
    public async Task RecoverUnfinishedRecordingsAsync_ShouldMarkOrphanedLogsAsInterruptedOrRecovered()
    {
        var (_, streamerRepo, logRepo) = CreateInMemoryRepos();

        var streamer = new Streamer { Username = "rraenee", IsTrackingEnabled = true };
        await streamerRepo.AddAsync(streamer);

        var orphanedLog = new RecordingLog
        {
            StreamerId = streamer.Id,
            StreamerUsername = streamer.Username,
            FilePath = "nonexistent.mp4",
            Status = RecordingStatus.Recording
        };
        await logRepo.AddAsync(orphanedLog);

        var streamerService = new MockStreamerServiceForOrchestrator();
        var mockEngine = new MockRecordingEngine();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var settingsService = new SettingsService(config, NullLogger<SettingsService>.Instance);
        var ffprobeValidator = new MockFFprobeValidator();

        var orchestrator = new RecordingOrchestrator(
            streamerService, 
            mockEngine, 
            logRepo, 
            settingsService, 
            ffprobeValidator,
            NullLogger<RecordingOrchestrator>.Instance);

        await orchestrator.RecoverUnfinishedRecordingsAsync();

        var recoveredLog = await logRepo.GetByIdAsync(orphanedLog.Id);
        Assert.NotNull(recoveredLog);
        Assert.True(recoveredLog.Status == RecordingStatus.Interrupted || recoveredLog.Status == RecordingStatus.Recovered);
        Assert.NotNull(recoveredLog.EndedAt);
    }
}
