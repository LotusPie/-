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
    bool IsPlaying)
{
    public bool HasIdentity =>
        !string.IsNullOrWhiteSpace(NormalizedTitle);
}
