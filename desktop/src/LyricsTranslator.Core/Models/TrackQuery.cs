namespace LyricsTranslator.Core.Models;

public sealed record TrackQuery(
    string DisplayTitle,
    string DisplayArtist,
    string? Album,
    TimeSpan? Duration,
    string NormalizedTitle,
    string NormalizedArtist,
    string CacheKey,
    string? SourceAppId,
    PlayerKind PlayerKind,
    bool IsPlaying,
    string? RecoveredTitle = null,
    string? RecoveredArtist = null)
{
    public bool HasIdentity =>
        !string.IsNullOrWhiteSpace(NormalizedTitle);

    public TrackQuery WithRecovered(string? title, string? artist) => this with
    {
        RecoveredTitle = string.IsNullOrWhiteSpace(title) ? RecoveredTitle : title.Trim(),
        RecoveredArtist = string.IsNullOrWhiteSpace(artist) ? RecoveredArtist : artist.Trim(),
    };
}
