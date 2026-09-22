using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.NowPlaying;

public sealed record NowPlayingSession(
    string SourceAppId,
    string? Title,
    string? Artist,
    string? Album,
    TimeSpan? Duration,
    TimeSpan Position,
    bool IsPlaying,
    PlayerKind PlayerKind)
{
    public bool HasTrackMetadata =>
        !string.IsNullOrWhiteSpace(Title);
}

public sealed record PlaybackProgress(TimeSpan Position, TimeSpan? Duration, bool IsPlaying);
