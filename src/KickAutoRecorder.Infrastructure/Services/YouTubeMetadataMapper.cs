using System;
using System.Collections.Generic;
using System.Linq;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Infrastructure.Services;

public class YouTubeMetadataMapper : IYouTubeMetadataMapper
{
    private static readonly Dictionary<string, string> CategoryMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Gaming", "20" },
        { "Oyun", "20" },
        { "Entertainment", "24" },
        { "Eğlence", "24" },
        { "People & Blogs", "22" },
        { "Kişiler ve Bloglar", "22" },
        { "Education", "27" },
        { "Eğitim", "27" },
        { "Science & Technology", "28" },
        { "Bilim ve Teknoloji", "28" },
        { "Music", "10" },
        { "Müzik", "10" },
        { "Sports", "17" },
        { "Spor", "17" }
    };

    private static readonly Dictionary<string, string> LanguageMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Turkish", "tr" },
        { "Türkçe", "tr" },
        { "English", "en" },
        { "İngilizce", "en" },
        { "German", "de" },
        { "Almanca", "de" },
        { "Spanish", "es" },
        { "İspanyolca", "es" }
    };

    public string MapCategoryToCategoryId(string? categoryName)
    {
        if (!string.IsNullOrWhiteSpace(categoryName) && CategoryMapping.TryGetValue(categoryName.Trim(), out var categoryId))
        {
            return categoryId;
        }

        return "20"; // Default Gaming
    }

    public string MapLanguageToLanguageCode(string? languageName)
    {
        if (!string.IsNullOrWhiteSpace(languageName) && LanguageMapping.TryGetValue(languageName.Trim(), out var langCode))
        {
            return langCode;
        }

        return "tr"; // Default Turkish
    }

    public string SanitizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Untitled Kick Recording";
        }

        var trimmed = title.Trim();
        if (trimmed.Length > 100)
        {
            trimmed = trimmed.Substring(0, 100);
        }

        return trimmed;
    }

    public string SanitizeDescription(string? description, string? streamerName, string? streamTitle)
    {
        var text = description ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(streamerName))
        {
            text = $"Original Stream: {streamTitle}\nStreamer: {streamerName}\nRecorded automatically with KickVideo.";
        }

        if (text.Length > 5000)
        {
            text = text.Substring(0, 5000);
        }

        return text;
    }

    public string[] ParseTags(string? tagsRaw)
    {
        if (string.IsNullOrWhiteSpace(tagsRaw))
        {
            return new[] { "kick", "stream", "recording" };
        }

        var list = tagsRaw.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(t => t.Trim())
                          .Where(t => t.Length > 0 && t.Length <= 50)
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .Take(50)
                          .ToArray();

        return list.Length > 0 ? list : new[] { "kick", "stream" };
    }
}
