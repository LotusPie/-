using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Lyrics;

/// <summary>
/// LRCLIB still supplies original/plain lyrics; timed LRC is overwritten from the
/// language-appropriate source (NetEase for JP/KR, filtered LRCLIB otherwise).
/// </summary>
public sealed class LanguageAwareLrclibClient : ILrclibClient
{
    private readonly ILrclibClient _lrclib;
    private readonly TimedLyricsRouter _timed;

    public LanguageAwareLrclibClient(ILrclibClient lrclib, TimedLyricsRouter timed)
    {
        _lrclib = lrclib;
        _timed = timed;
    }

    public async Task<LrclibTrack?> FindAsync(TrackQuery query, CancellationToken cancellationToken)
    {
        LrclibTrack? catalog = null;
        try
        {
            catalog = await _lrclib.FindAsync(query, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Timed source may still supply karaoke LRC.
        }

        TimedLyricsHit? timed = null;
        try
        {
            timed = await _timed.FindAsync(query, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Keep catalog original if timed lookup fails.
        }

        var language = LyricLanguageDetector.Detect(query);
        if (timed is not null)
        {
            if (catalog is null)
            {
                return new LrclibTrack
                {
                    TrackName = timed.TrackName,
                    ArtistName = timed.ArtistName,
                    Duration = timed.DurationSeconds,
                    SyncedLyrics = timed.SyncedLyrics,
                };
            }

            catalog.SyncedLyrics = timed.SyncedLyrics;
            catalog.Instrumental = false;
            return catalog;
        }

        if (catalog is not null &&
            !string.IsNullOrWhiteSpace(catalog.SyncedLyrics) &&
            !LrcLanguageFilter.Fits(catalog.SyncedLyrics, language))
        {
            catalog.SyncedLyrics = null;
        }

        return catalog;
    }
}
