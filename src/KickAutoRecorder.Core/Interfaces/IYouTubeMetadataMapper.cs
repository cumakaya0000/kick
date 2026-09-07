namespace KickAutoRecorder.Core.Interfaces;

public interface IYouTubeMetadataMapper
{
    string MapCategoryToCategoryId(string? categoryName);
    string MapLanguageToLanguageCode(string? languageName);
    string SanitizeTitle(string? title);
    string SanitizeDescription(string? description, string? streamerName, string? streamTitle);
    string[] ParseTags(string? tagsRaw);
}
