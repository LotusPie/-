using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Lyrics;

/// <summary>
/// LRCLIB timed fallback: keep only records whose title/LRC match the song language.
/// </summary>
public sealed class LrclibTimedLyricsSource : ITimedLyricsSource
{
    public const string NameValue = "LRCLIB";

    private readonly LrclibClient _lrclib;

    public LrclibTimedLyricsSource(LrclibClient lrclib)
    {
        _lrclib = lrclib;
    }

    public string Name => NameValue;

    public async Task<TimedLyricsHit?> FindAsync(TrackQuery query, LyricLanguage language, CancellationToken cancellationToken)
    {
        if (!query.HasIdentity)
        {
            return null;
        }

        IReadOnlyList<LrclibTrack> results;
        try
        {
            results = await _lrclib.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }

        var hit = LrclibClient.RankTimed(results, query).FirstOrDefault();
        if (hit is null || string.IsNullOrWhiteSpace(hit.SyncedLyrics))
        {
            return null;
        }

        var cleaned = LrcLanguageFilter.Clean(hit.SyncedLyrics);
        if (cleaned is null || !LrcLanguageFilter.Fits(cleaned, language))
        {
            return null;
        }

        return new TimedLyricsHit(cleaned, NameValue, hit.TrackName, hit.ArtistName, hit.Duration);
    }
}
