using System.Collections.Generic;

namespace KickAutoRecorder.Core.Models;

public class YouTubeUploadValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();

    public string ErrorSummary => string.Join("; ", Errors);
}
