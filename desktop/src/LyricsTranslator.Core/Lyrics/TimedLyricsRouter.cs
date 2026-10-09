using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Lyrics;

/// <summary>
/// Language-first timed lyrics: JP/KR NetEase, then language-filtered LRCLIB.
/// English never uses the Japanese NetEase path.
/// </summary>
public sealed class TimedLyricsRouter
{
    private readonly ITimedLyricsSource _netease;
    private readonly ITimedLyricsSource _lrclib;

    public TimedLyricsRouter(ITimedLyricsSource netease, ITimedLyricsSource lrclib)
    {
        _netease = netease;
        _lrclib = lrclib;
    }

    public IReadOnlyList<string> SourcesFor(LyricLanguage language) => language switch
    {
        LyricLanguage.Japanese => [_netease.Name, _lrclib.Name],
        LyricLanguage.Korean => [_netease.Name, _lrclib.Name],
        _ => [_lrclib.Name],
    };

    public async Task<TimedLyricsHit?> FindAsync(TrackQuery query, CancellationToken cancellationToken)
    {
        if (!query.HasIdentity)
        {
            return null;
        }

        var language = LyricLanguageDetector.Detect(query);
        foreach (var source in Sources(language))
        {
            TimedLyricsHit? hit;
            try
            {
                hit = await source.FindAsync(query, language, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                continue;
            }

            if (hit is not null &&
                LrcLanguageFilter.Fits(hit.SyncedLyrics, language))
            {
                return hit;
            }
        }

        return null;
    }

    private IEnumerable<ITimedLyricsSource> Sources(LyricLanguage language)
    {
        if (language is LyricLanguage.Japanese or LyricLanguage.Korean)
        {
            yield return _netease;
        }

        yield return _lrclib;
    }
}
