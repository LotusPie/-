using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Providers;

public sealed class LrclibTimedProvider : ILyricProvider
{
    public const string ProviderKey = "lrclib-timed";

    private readonly LrclibCatalog _catalog;

    public LrclibTimedProvider(LrclibCatalog catalog)
    {
        _catalog = catalog;
    }

    public string Key => ProviderKey;

    public string DisplayName => "LRCLIB LRC";

    public LyricLayer Layer => LyricLayer.Timed;

    public ProviderSyncType SyncType => ProviderSyncType.Line;

    public async Task<LyricProviderResult?> FetchAsync(ProviderContext context)
    {
        var track = await _catalog.GetAsync(context).ConfigureAwait(false);
        if (track is null || string.IsNullOrWhiteSpace(track.SyncedLyrics))
        {
            return null;
        }

        return new LyricProviderResult
        {
            ProviderKey = Key,
            Source = LyricsSource.Lrclib,
            Text = track.SyncedLyrics,
            TrackName = track.TrackName,
            ArtistName = track.ArtistName,
            LrclibId = track.Id == 0 ? null : track.Id,
        };
    }
}
