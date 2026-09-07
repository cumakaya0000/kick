using System;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using KickAutoRecorder.App.ViewModels;
using KickAutoRecorder.App.Views;
using KickAutoRecorder.Core.Interfaces;
using KickAutoRecorder.Infrastructure.Kick;
using KickAutoRecorder.Infrastructure.Persistence;
using KickAutoRecorder.Infrastructure.Repositories;
using KickAutoRecorder.Infrastructure.Services;
using KickAutoRecorder.Infrastructure.Storage;
using KickAutoRecorder.Recording.Services;
using KickAutoRecorder.Worker.Services;

namespace KickAutoRecorder.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var baseDir = AppContext.BaseDirectory;
        Directory.SetCurrentDirectory(baseDir);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(baseDir)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .CreateLogger();

        try
        {
            Log.Information("Starting KickVideo App Host Initialization...");

            _host = Host.CreateDefaultBuilder(e.Args)
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    var connectionString = context.Configuration.GetConnectionString("DefaultConnection")
                                           ?? "Data Source=kickvideodata.db";

                    services.AddDbContextFactory<AppDbContext>(options =>
                        options.UseSqlite(connectionString));

                    // Repositories & Services
                    services.AddSingleton<ISettingsService, SettingsService>();
                    services.AddSingleton<INotificationService, NotificationService>();
                    services.AddSingleton<IFFprobeValidator, FFprobeValidator>();
                    services.AddTransient<IStreamerRepository, StreamerRepository>();
                    services.AddTransient<IRecordingLogRepository, RecordingLogRepository>();
                    services.AddTransient<IVideoJobRepository, VideoJobRepository>();
                    services.AddTransient<IYouTubeMetadataDraftRepository, YouTubeMetadataDraftRepository>();
                    services.AddTransient<IStreamerService, StreamerService>();
                    services.AddSingleton<IDiskService, DiskService>();
                    services.AddTransient<IVideoPreparationService, VideoPreparationService>();
                    services.AddTransient<IThumbnailGeneratorService, FFmpegThumbnailGenerator>();
                    
                    // Recording Engine & Render Engine
                    services.AddSingleton<IFFmpegLocator, FFmpegLocator>();
                    services.AddSingleton<IRecordingEngine, FFmpegRecordingEngine>();
                    services.AddSingleton<IVideoRenderEngine, FFmpegVideoRenderEngine>();

                    // Video Job Queue & Worker
                    services.AddSingleton<IVideoJobQueue, VideoJobQueue>();
                    services.AddHostedService<VideoJobQueueWorker>();

                    // Recording Orchestrator
                    services.AddTransient<IRecordingOrchestrator, RecordingOrchestrator>();

                    // YouTube Publishing Services & Worker
                    services.AddSingleton<IYouTubeCredentialStore, WindowsYouTubeCredentialStore>();
                    services.AddSingleton<IYouTubeAuthService, YouTubeAuthService>();
                    services.AddSingleton<IYouTubeMetadataMapper, YouTubeMetadataMapper>();
                    services.AddTransient<IYouTubeUploadService, YouTubeUploadService>();
                    services.AddTransient<IYouTubeUploadRepository, YouTubeUploadRepository>();
                    services.AddSingleton<IYouTubeUploadQueue, YouTubeUploadQueue>();
                    services.AddHostedService<YouTubeUploadWorker>();

                    // Typed HttpClient for Kick API
                    services.AddHttpClient<IKickApiClient, KickApiClient>(client =>
                    {
                        client.Timeout = TimeSpan.FromSeconds(10);
                    });

                    // Background Worker
                    services.AddHostedService<StreamMonitoringWorker>();

                    // MVVM ViewModels & Views
                    services.AddTransient<StreamersViewModel>();
                    services.AddTransient<RecordingsViewModel>();
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<VideoStudioViewModel>();
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<KickAutoRecorder.App.Views.MainWindow>();
                })
                .Build();

            // 1. Ensure SQLite DB Schema Created BEFORE background worker starts
            using (var scope = _host.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.EnsureCreatedAsync();
                try
                {
                    await dbContext.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                }
                catch
                {
                    // WAL mode failover ignore
                }

                var alterStatements = new[]
                {
                    "ALTER TABLE Streamers ADD COLUMN PreferredQuality TEXT DEFAULT 'En Yüksek (Kaynak)';",
                    "ALTER TABLE Streamers ADD COLUMN LastLiveAt TEXT NULL;",
                    "ALTER TABLE Streamers ADD COLUMN LastOfflineAt TEXT NULL;",
                    "ALTER TABLE RecordingLogs ADD COLUMN IsValidated INTEGER DEFAULT 0;",
                    "ALTER TABLE RecordingLogs ADD COLUMN VideoCodec TEXT NULL;",
                    "ALTER TABLE RecordingLogs ADD COLUMN AudioCodec TEXT NULL;",
                    "ALTER TABLE RecordingLogs ADD COLUMN Resolution TEXT NULL;",
                    "ALTER TABLE RecordingLogs ADD COLUMN Duration TEXT NULL;",
                    @"CREATE TABLE IF NOT EXISTS VideoJobs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        RecordingLogId INTEGER NOT NULL,
                        StreamerUsername TEXT NOT NULL DEFAULT '',
                        Title TEXT NOT NULL DEFAULT '',
                        Description TEXT NULL,
                        Tags TEXT NULL,
                        SegmentPathsJson TEXT NOT NULL DEFAULT '[]',
                        TrimStartTimeSeconds REAL NULL,
                        TrimEndTimeSeconds REAL NULL,
                        ExportPreset TEXT NOT NULL DEFAULT 'FastCopy',
                        TargetResolution TEXT NULL,
                        TargetFps INTEGER NULL,
                        AudioBitrateKbps INTEGER NOT NULL DEFAULT 256,
                        OutputPath TEXT NOT NULL DEFAULT '',
                        ThumbnailPath TEXT NULL,
                        FileSizeBytes INTEGER NOT NULL DEFAULT 0,
                        Status INTEGER NOT NULL DEFAULT 0,
                        ProgressPercentage REAL NOT NULL DEFAULT 0,
                        ErrorMessage TEXT NULL,
                        CreatedAt TEXT NOT NULL,
                        StartedAt TEXT NULL,
                        CompletedAt TEXT NULL,
                        FOREIGN KEY(RecordingLogId) REFERENCES RecordingLogs(Id) ON DELETE CASCADE
                    );",
                    @"CREATE TABLE IF NOT EXISTS YouTubeVideoDrafts (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        RecordingLogId INTEGER NOT NULL,
                        VideoJobId INTEGER NULL,
                        StreamerUsername TEXT NOT NULL DEFAULT '',
                        Title TEXT NOT NULL DEFAULT '',
                        Description TEXT NOT NULL DEFAULT '',
                        Tags TEXT NOT NULL DEFAULT '',
                        Category TEXT NOT NULL DEFAULT 'Gaming',
                        Language TEXT NOT NULL DEFAULT 'Turkish',
                        GameName TEXT NULL,
                        ThumbnailPath TEXT NULL,
                        PrivacyStatus TEXT NOT NULL DEFAULT 'Private',
                        PlaylistName TEXT NULL,
                        MadeForKids INTEGER NOT NULL DEFAULT 0,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NULL,
                        FOREIGN KEY(RecordingLogId) REFERENCES RecordingLogs(Id) ON DELETE CASCADE,
                        FOREIGN KEY(VideoJobId) REFERENCES VideoJobs(Id) ON DELETE SET NULL
                    );",
                    @"CREATE TABLE IF NOT EXISTS YouTubeUploadJobs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        RenderJobId INTEGER NULL,
                        RecordingLogId INTEGER NULL,
                        VideoFilePath TEXT NOT NULL DEFAULT '',
                        ThumbnailPath TEXT NULL,
                        Title TEXT NOT NULL DEFAULT '',
                        Description TEXT NULL,
                        Tags TEXT NULL,
                        CategoryId TEXT NOT NULL DEFAULT '20',
                        DefaultLanguage TEXT NOT NULL DEFAULT 'tr',
                        PrivacyStatus TEXT NOT NULL DEFAULT 'Private',
                        MadeForKids INTEGER NOT NULL DEFAULT 0,
                        PlaylistId TEXT NULL,
                        Status INTEGER NOT NULL DEFAULT 0,
                        ProgressPercent REAL NOT NULL DEFAULT 0,
                        UploadSessionUri TEXT NULL,
                        UploadedBytes INTEGER NOT NULL DEFAULT 0,
                        TotalBytes INTEGER NOT NULL DEFAULT 0,
                        UploadSessionCreatedAt TEXT NULL,
                        LastProgressAt TEXT NULL,
                        YouTubeVideoId TEXT NULL,
                        YouTubeVideoUrl TEXT NULL,
                        RetryCount INTEGER NOT NULL DEFAULT 0,
                        LastError TEXT NULL,
                        ThumbnailWarning TEXT NULL,
                        PlaylistWarning TEXT NULL,
                        CreatedAt TEXT NOT NULL,
                        StartedAt TEXT NULL,
                        CompletedAt TEXT NULL,
                        FOREIGN KEY(RecordingLogId) REFERENCES RecordingLogs(Id) ON DELETE SET NULL,
                        FOREIGN KEY(RenderJobId) REFERENCES VideoJobs(Id) ON DELETE SET NULL
                    );"
                };

                foreach (var stmt in alterStatements)
                {
                    try
                    {
                        await dbContext.Database.ExecuteSqlRawAsync(stmt);
                    }
                    catch (Exception sqlEx)
                    {
                        Log.Warning("Schema statement execution warning: {Error}", sqlEx.Message);
                    }
                }

                Log.Information("SQLite Database schema verified/initialized.");
            }

            // 2. Show UI MainWindow
            var mainWindow = _host.Services.GetRequiredService<KickAutoRecorder.App.Views.MainWindow>();
            mainWindow.Show();

            // 3. Start background monitoring worker host
            await _host.StartAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application Startup failed fatally.");
            MessageBox.Show($"Startup Error: {ex.Message}", "KickVideo Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
