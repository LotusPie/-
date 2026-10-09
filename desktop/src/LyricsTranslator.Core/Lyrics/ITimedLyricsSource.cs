using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Lyrics;

public sealed record TimedLyricsHit(
    string SyncedLyrics,
    string SourceName,
    string? TrackName,
    string? ArtistName,
    double DurationSeconds);

public interface ITimedLyricsSource
{
    string Name { get; }

    Task<TimedLyricsHit?> FindAsync(TrackQuery query, LyricLanguage language, CancellationToken cancellationToken);
}
