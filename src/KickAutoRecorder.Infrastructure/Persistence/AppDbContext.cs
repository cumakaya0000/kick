using Microsoft.EntityFrameworkCore;
using KickAutoRecorder.Core.Entities;

namespace KickAutoRecorder.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Streamer> Streamers => Set<Streamer>();
    public DbSet<RecordingLog> RecordingLogs => Set<RecordingLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<VideoJob> VideoJobs => Set<VideoJob>();
    public DbSet<YouTubeVideoDraft> YouTubeVideoDrafts => Set<YouTubeVideoDraft>();
    public DbSet<YouTubeUploadJob> YouTubeUploadJobs => Set<YouTubeUploadJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Streamer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.ChannelUrl).IsRequired().HasMaxLength(255);
        });

        modelBuilder.Entity<RecordingLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StreamerUsername).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);

            entity.HasOne(e => e.Streamer)
                  .WithMany()
                  .HasForeignKey(e => e.StreamerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(1000);
        });

        modelBuilder.Entity<VideoJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StreamerUsername).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(250);
            entity.Property(e => e.OutputPath).IsRequired().HasMaxLength(500);

            entity.HasOne(e => e.RecordingLog)
                  .WithMany()
                  .HasForeignKey(e => e.RecordingLogId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<YouTubeVideoDraft>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(5000);

            entity.HasOne(e => e.RecordingLog)
                  .WithMany()
                  .HasForeignKey(e => e.RecordingLogId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.VideoJob)
                  .WithMany()
                  .HasForeignKey(e => e.VideoJobId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<YouTubeUploadJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(250);
            entity.Property(e => e.VideoFilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PrivacyStatus).IsRequired().HasMaxLength(50);

            entity.HasOne(e => e.RecordingLog)
                  .WithMany()
                  .HasForeignKey(e => e.RecordingLogId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.RenderJob)
                  .WithMany()
                  .HasForeignKey(e => e.RenderJobId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
