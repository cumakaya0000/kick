using System;
using System.Threading;
using System.Threading.Tasks;
using KickAutoRecorder.Core.Entities;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Core.Interfaces;

public interface IRecordingEngine
{
    event Action<RecordingHealthTelemetry>? OnTelemetryUpdated;
    Task<RecordingLog> StartRecordingAsync(Streamer streamer, string streamUrl, string outputDirectory, CancellationToken cancellationToken = default);
    Task StopRecordingAsync(int recordingLogId, CancellationToken cancellationToken = default);
    bool IsRecording(int streamerId);
    RecordingHealthTelemetry? GetActiveTelemetry(int streamerId);
}
