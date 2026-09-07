using System.Text.Json.Serialization;

namespace KickAutoRecorder.Infrastructure.Kick;

public class KickChannelResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("playback_url")]
    public string? PlaybackUrl { get; set; }

    [JsonPropertyName("livestream")]
    public KickLivestreamResponse? Livestream { get; set; }

    [JsonPropertyName("user")]
    public KickUserResponse? User { get; set; }
}

public class KickLivestreamResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("session_title")]
    public string? SessionTitle { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("viewer_count")]
    public int ViewerCount { get; set; }

    [JsonPropertyName("viewers")]
    public int Viewers { get; set; }

    [JsonPropertyName("category")]
    public KickCategoryResponse? Category { get; set; }

    [JsonPropertyName("categories")]
    public KickCategoryResponse[]? Categories { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }
}

public class KickCategoryResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;
}

public class KickUserResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("profile_pic")]
    public string? ProfilePic { get; set; }
}
