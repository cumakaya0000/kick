using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using KickAutoRecorder.Core.Models;
using KickAutoRecorder.Infrastructure.Services;

namespace KickAutoRecorder.IntegrationTests;

public class SettingsServiceTests
{
    [Fact]
    public async Task UpdateSettingsAsync_PersistsNewSettingsCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var logger = NullLogger<SettingsService>.Instance;
        var service = new SettingsService(configuration, logger);

        var newSettings = new RecordingSettings
        {
            OutputFolder = "CustomRecordingsFolder",
            FFmpegPath = "C:\\CustomPath\\ffmpeg.exe",
            PollingIntervalSeconds = 45,
            MinDiskSpaceGB = 10
        };

        // Act
        await service.UpdateSettingsAsync(newSettings);
        var loaded = service.GetSettings();

        // Assert
        Assert.Equal("CustomRecordingsFolder", loaded.OutputFolder);
        Assert.Equal("C:\\CustomPath\\ffmpeg.exe", loaded.FFmpegPath);
        Assert.Equal(45, loaded.PollingIntervalSeconds);
        Assert.Equal(10, loaded.MinDiskSpaceGB);
    }
}
