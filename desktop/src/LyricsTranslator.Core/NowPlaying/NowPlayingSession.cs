using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.NowPlaying;

public sealed record NowPlayingSession(
    string SourceAppId,
    string? Title,
    string? Artist,
    string? Album,
    TimeSpan? Duration,
    bool IsPlaying,
    PlayerKind PlayerKind)
{
    public bool HasTrackMetadata =>
        !string.IsNullOrWhiteSpace(Title);
}
