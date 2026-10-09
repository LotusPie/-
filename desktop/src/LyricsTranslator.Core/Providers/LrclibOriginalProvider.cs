using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Providers;

public sealed class LrclibOriginalProvider : ILyricProvider
{
    public const string ProviderKey = "lrclib-original";

    private readonly LrclibCatalog _catalog;

    public LrclibOriginalProvider(LrclibCatalog catalog)
    {
        _catalog = catalog;
    }

    public string Key => ProviderKey;

    public string DisplayName => "LRCLIB 原文";

    public LyricLayer Layer => LyricLayer.Original;

    public ProviderSyncType SyncType => ProviderSyncType.Unsynced;

    public async Task<LyricProviderResult?> FetchAsync(ProviderContext context)
    {
        var track = await _catalog.GetAsync(context).ConfigureAwait(false);
        if (track is null)
        {
            return null;
        }

        var plain = track.EffectivePlainLyrics;
        var instrumental = track.Instrumental && string.IsNullOrWhiteSpace(plain);
        if (!instrumental && string.IsNullOrWhiteSpace(plain))
        {
            return null;
        }

        return new LyricProviderResult
        {
            ProviderKey = Key,
            Source = LyricsSource.Lrclib,
            Text = plain,
            TrackName = track.TrackName,
            ArtistName = track.ArtistName,
            LrclibId = track.Id == 0 ? null : track.Id,
            Instrumental = instrumental,
        };
    }
}
