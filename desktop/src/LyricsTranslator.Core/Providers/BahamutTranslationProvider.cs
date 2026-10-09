using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Providers;

public sealed class BahamutTranslationProvider : ILyricProvider
{
    public const string ProviderKey = "bahamut";

    private readonly IBahamutClient _bahamut;

    public BahamutTranslationProvider(IBahamutClient bahamut)
    {
        _bahamut = bahamut;
    }

    public string Key => ProviderKey;

    public string DisplayName => "巴哈姆特";

    public LyricLayer Layer => LyricLayer.Translation;

    public ProviderSyncType SyncType => ProviderSyncType.Unsynced;

    public async Task<LyricProviderResult?> FetchAsync(ProviderContext context)
    {
        var hit = await _bahamut.FindAsync(context.Query, context.CancellationToken).ConfigureAwait(false);
        if (hit is null || string.IsNullOrWhiteSpace(hit.Translation))
        {
            return null;
        }

        return new LyricProviderResult
        {
            ProviderKey = Key,
            Source = LyricsSource.Bahamut,
            Text = hit.Translation,
            SourceTitle = hit.SourceTitle,
            SourceHref = hit.Url,
            SiteLabel = hit.SiteLabel,
        };
    }
}
