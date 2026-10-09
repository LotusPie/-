using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;
using LyricsTranslator.Core.Providers;

namespace LyricsTranslator.Core.Tests;

public class ProviderChainTests
{
    [Fact]
    public async Task Translation_chain_takes_bahamut_before_web()
    {
        var bahamut = new StubTranslation("bahamut", LyricsSource.Bahamut, "繁中一行");
        var web = new StubTranslation("web", LyricsSource.Web, "不該出現");
        var chain = ProviderChain.Create(LyricLayer.Translation, bahamut, web);
        var ctx = Context();

        var hit = await chain.FetchAsync(ctx);

        Assert.NotNull(hit);
        Assert.Equal("bahamut", hit!.ProviderKey);
        Assert.Equal("繁中一行", hit.Text);
        Assert.True(bahamut.WasCalled);
        Assert.False(web.WasCalled);
    }

    [Fact]
    public async Task Thrown_provider_is_skipped()
    {
        var chain = ProviderChain.Create(
            LyricLayer.Translation,
            new ThrowingProvider(),
            new StubTranslation("web", LyricsSource.Web, "網頁譯文"));

        var hit = await chain.FetchAsync(Context());

        Assert.Equal("web", hit?.ProviderKey);
        Assert.Equal("網頁譯文", hit?.Text);
    }

    [Fact]
    public async Task Translation_and_timed_layers_do_not_first_win_each_other()
    {
        var lookup = new LyricsLookup(
            ProviderChain.Create(
                LyricLayer.Translation,
                new StubTranslation("bahamut", LyricsSource.Bahamut, "在夜裡往前衝")),
            ProviderChain.Create(
                LyricLayer.Original,
                new StubTranslation("lrclib-original", LyricsSource.Lrclib, "夜に駆ける") { Layer = LyricLayer.Original }),
            ProviderChain.Create(
                LyricLayer.Timed,
                new StubTranslation("timed", LyricsSource.Lrclib, "[00:10.00] 夜に駆ける")
                {
                    Layer = LyricLayer.Timed,
                    SyncType = ProviderSyncType.Line,
                }));

        var ctx = Context();
        var translation = await lookup.Translation.FetchAsync(ctx);
        var original = await lookup.Original.FetchAsync(ctx);
        var timed = await lookup.Timed.FetchAsync(ctx);

        Assert.Equal("在夜裡往前衝", translation?.Text);
        Assert.Equal("夜に駆ける", original?.Text);
        Assert.Equal("[00:10.00] 夜に駆ける", timed?.Text);
    }

    [Fact]
    public async Task Shared_lrclib_catalog_is_fetched_once_for_original_and_timed()
    {
        var lrclib = new CountingLrclib(new LrclibTrack
        {
            Id = 3,
            TrackName = "Hello",
            ArtistName = "Adele",
            PlainLyrics = "Hello from the other side",
            SyncedLyrics = "[00:12.00] Hello from the other side",
        });
        var lookup = LyricsLookup.FromClients(lrclib, new MissBahamut(), new MissWeb());
        var ctx = Context("Hello", "Adele");

        var original = await lookup.Original.FetchAsync(ctx);
        var timed = await lookup.Timed.FetchAsync(ctx);

        Assert.Equal(1, lrclib.Calls);
        Assert.Equal("Hello from the other side", original?.Text);
        Assert.Equal("[00:12.00] Hello from the other side", timed?.Text);
    }

    private static ProviderContext Context(string title = "夜に駆ける", string artist = "YOASOBI") =>
        new(TrackNormalizer.FromRaw(title, artist, null, TimeSpan.FromSeconds(200), "Chrome", PlayerKind.Browser, true), CancellationToken.None);

    private sealed class StubTranslation : ILyricProvider
    {
        private readonly LyricsSource _source;
        private readonly string _text;

        public StubTranslation(string key, LyricsSource source, string text)
        {
            Key = key;
            _source = source;
            _text = text;
        }

        public string Key { get; }

        public string DisplayName => Key;

        public LyricLayer Layer { get; init; } = LyricLayer.Translation;

        public ProviderSyncType SyncType { get; init; } = ProviderSyncType.Unsynced;

        public bool WasCalled { get; private set; }

        public Task<LyricProviderResult?> FetchAsync(ProviderContext context)
        {
            WasCalled = true;
            return Task.FromResult<LyricProviderResult?>(new LyricProviderResult
            {
                ProviderKey = Key,
                Source = _source,
                Text = _text,
            });
        }
    }

    private sealed class ThrowingProvider : ILyricProvider
    {
        public string Key => "boom";

        public string DisplayName => Key;

        public LyricLayer Layer => LyricLayer.Translation;

        public ProviderSyncType SyncType => ProviderSyncType.Unsynced;

        public Task<LyricProviderResult?> FetchAsync(ProviderContext context) =>
            throw new HttpRequestException("timeout");
    }

    private sealed class CountingLrclib(LrclibTrack track) : ILrclibClient
    {
        public int Calls { get; private set; }

        public Task<LrclibTrack?> FindAsync(TrackQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<LrclibTrack?>(track);
        }
    }

    private sealed class MissBahamut : IBahamutClient
    {
        public Task<CommunityTranslation?> FindAsync(TrackQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CommunityTranslation?>(null);
    }

    private sealed class MissWeb : IWebLyricsClient
    {
        public Task<CommunityTranslation?> FindAsync(TrackQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CommunityTranslation?>(null);
    }
}
