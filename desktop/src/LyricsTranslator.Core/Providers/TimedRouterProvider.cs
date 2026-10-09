using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Providers;

/// <summary>
/// Language-specific timed chain: JP/KR NetEase then filtered LRCLIB.
/// English never takes the Japanese NetEase path.
/// </summary>
public sealed class TimedRouterProvider : ILyricProvider
{
    public const string ProviderKey = "timed-router";

    private readonly TimedLyricsRouter _router;

    public TimedRouterProvider(TimedLyricsRouter router)
    {
        _router = router;
    }

    public string Key => ProviderKey;

    public string DisplayName => "語言 LRC";

    public LyricLayer Layer => LyricLayer.Timed;

    public ProviderSyncType SyncType => ProviderSyncType.Line;

    public async Task<LyricProviderResult?> FetchAsync(ProviderContext context)
    {
        var hit = await _router.FindAsync(context.Query, context.CancellationToken).ConfigureAwait(false);
        if (hit is null || string.IsNullOrWhiteSpace(hit.SyncedLyrics))
        {
            return null;
        }

        return new LyricProviderResult
        {
            ProviderKey = Key,
            Source = LyricsSource.Lrclib,
            Text = hit.SyncedLyrics,
            TrackName = hit.TrackName,
            ArtistName = hit.ArtistName,
        };
    }
}
