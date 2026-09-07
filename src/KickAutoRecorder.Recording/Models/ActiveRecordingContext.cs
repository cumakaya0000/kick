using System;
using System.Diagnostics;
using System.Threading;
using KickAutoRecorder.Core.Models;

namespace KickAutoRecorder.Recording.Models;

public class ActiveRecordingContext
{
    public int RecordingLogId { get; set; }
    public int StreamerId { get; set; }
    public string StreamerUsername { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string SessionFolder { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public Process Process { get; set; } = null!;
    public CancellationTokenSource CancellationTokenSource { get; set; } = null!;
    public RecordingHealthTelemetry Telemetry { get; set; } = new();
    public int ReconnectAttempts { get; set; }
}
