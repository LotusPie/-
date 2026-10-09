using LyricsTranslator.Core.Lyrics;

namespace LyricsTranslator.Core.Providers;

/// <summary>
/// Three independent chains composed after fetch. Translation never competes
/// with timed original for "first usable lyrics".
/// </summary>
public sealed class LyricsLookup
{
    public LyricsLookup(ProviderChain translation, ProviderChain original, ProviderChain timed)
    {
        Translation = translation;
        Original = original;
        Timed = timed;
    }

    public ProviderChain Translation { get; }

    public ProviderChain Original { get; }

    public ProviderChain Timed { get; }

    /// <summary>
    /// Tests and the compatibility pipeline ctor: one <see cref="ILrclibClient"/>
    /// supplies both original and timed via a shared catalog memo.
    /// </summary>
    public static LyricsLookup FromClients(ILrclibClient lrclib, IBahamutClient bahamut, IWebLyricsClient web)
    {
        var catalog = new LrclibCatalog(lrclib);
        return new LyricsLookup(
            ProviderChain.Create(
                LyricLayer.Translation,
                new BahamutTranslationProvider(bahamut),
                new WebTranslationProvider(web)),
            ProviderChain.Create(
                LyricLayer.Original,
                new LrclibOriginalProvider(catalog)),
            ProviderChain.Create(
                LyricLayer.Timed,
                new LrclibTimedProvider(catalog)));
    }

    /// <summary>
    /// App composition: LRCLIB for original/plain, TimedLyricsRouter for karaoke LRC.
    /// </summary>
    public static LyricsLookup Create(
        IBahamutClient bahamut,
        IWebLyricsClient web,
        ILrclibClient originalLrclib,
        TimedLyricsRouter timed)
    {
        var catalog = new LrclibCatalog(originalLrclib);
        return new LyricsLookup(
            ProviderChain.Create(
                LyricLayer.Translation,
                new BahamutTranslationProvider(bahamut),
                new WebTranslationProvider(web)),
            ProviderChain.Create(
                LyricLayer.Original,
                new LrclibOriginalProvider(catalog)),
            ProviderChain.Create(
                LyricLayer.Timed,
                new TimedRouterProvider(timed)));
    }
}
