using System;
using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Core.Interfaces;

public interface IVideoRenderEngine
{
    event Action<VideoRenderProgress>? OnRenderProgress;

    /// <summary>
    /// Executes rendering/processing for a VideoJob atomically.
    /// Tries fast copy demuxer first, falls back to transcoding if necessary or requested by preset.
    /// Writes to .tmp.mp4 and validates output via FFprobe before finalizing.
    /// </summary>
    Task ExecuteRenderJobAsync(VideoJob job, CancellationToken cancellationToken = default);
}
