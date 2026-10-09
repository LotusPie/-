using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Providers;

public sealed class WebTranslationProvider : ILyricProvider
{
    public const string ProviderKey = "web";

    private readonly IWebLyricsClient _web;

    public WebTranslationProvider(IWebLyricsClient web)
    {
        _web = web;
    }

    public string Key => ProviderKey;

    public string DisplayName => "網頁";

    public LyricLayer Layer => LyricLayer.Translation;

    public ProviderSyncType SyncType => ProviderSyncType.Unsynced;

    public async Task<LyricProviderResult?> FetchAsync(ProviderContext context)
    {
        var hit = await _web.FindAsync(context.Query, context.CancellationToken).ConfigureAwait(false);
        if (hit is null || string.IsNullOrWhiteSpace(hit.Translation))
        {
            return null;
        }

        return new LyricProviderResult
        {
            ProviderKey = Key,
            Source = BahamutParser.SourceFromSite(hit.SiteLabel),
            Text = hit.Translation,
            SourceTitle = hit.SourceTitle,
            SourceHref = hit.Url,
            SiteLabel = hit.SiteLabel,
        };
    }
}
