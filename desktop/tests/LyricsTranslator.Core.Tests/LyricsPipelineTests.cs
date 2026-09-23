using LyricsTranslator.Core.Cache;
using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;
using LyricsTranslator.Core.Pipeline;
using LyricsTranslator.Core.Settings;
using LyricsTranslator.Core.Translation;

namespace LyricsTranslator.Core.Tests;

public class LyricsPipelineTests
{
    [Fact]
    public async Task Does_not_invent_lyrics_when_lookup_misses()
    {
        var translator = new RecordingTranslator();
        var pipeline = Create(new MissLrclib(), translator, apiKey: "sk-test");
        var query = Song("Unknown Song", "Nobody");

        var result = await pipeline.ResolveAsync(query, CancellationToken.None);

        Assert.Equal(LyricsStatus.NeedsPaste, result.Status);
        Assert.Null(result.OriginalLyrics);
        Assert.Null(result.Translation);
        Assert.False(translator.WasCalled);
        Assert.Contains("發明", result.Message);
    }

    [Fact]
    public async Task Uses_lrclib_original_then_ai_and_labels_source()
    {
        var translator = new RecordingTranslator { Translation = "繁中一行" };
        var pipeline = Create(
            new StubLrclib(new LrclibTrack
            {
                Id = 9,
                TrackName = "Hello",
                ArtistName = "Adele",
                Duration = 295,
                PlainLyrics = "Hello from the other side",
                SyncedLyrics = "[00:12.00] Hello from the other side",
            }),
            translator,
            apiKey: "sk-test");

        var result = await pipeline.ResolveAsync(Song("Hello", "Adele"), CancellationToken.None);

        Assert.Equal(LyricsStatus.Ready, result.Status);
        Assert.Equal("Hello from the other side", result.OriginalLyrics);
        Assert.Equal("繁中一行", result.Translation);
        Assert.Equal("社群／LRCLIB → AI", result.SourceLabel);
        Assert.Equal("[00:12.00] Hello from the other side", result.SyncedLyrics);
        Assert.True(translator.WasCalled);
        Assert.Contains("Adele", translator.LastRequest!.Query.DisplayArtist);
        Assert.Contains("Hello from the other side", translator.LastRequest.OriginalLyrics);
    }

    [Fact]
    public async Task English_song_uses_bahamut_before_ai()
    {
        var translator = new RecordingTranslator { Translation = "不該出現" };
        var bahamut = new StubBahamut(new CommunityTranslation(
            "你好來自另一邊\n我必須說\n我已試過\n告訴你一切",
            "Hello Adele 歌詞翻譯",
            "https://home.gamer.com.tw/artwork.php?sn=9"));
        var pipeline = Create(
            new StubLrclib(new LrclibTrack
            {
                TrackName = "Hello",
                ArtistName = "Adele",
                PlainLyrics = "Hello from the other side",
                SyncedLyrics = "[00:12.00] Hello from the other side",
            }),
            translator,
            apiKey: "sk-test",
            bahamut);

        var result = await pipeline.ResolveAsync(Song("Hello", "Adele"), CancellationToken.None);

        Assert.False(translator.WasCalled);
        Assert.Equal(LyricsSource.Bahamut, result.TranslationSource);
        Assert.Equal("社群／LRCLIB → 巴哈姆特", result.SourceLabel);
        Assert.Contains("你好來自另一邊", result.Translation);
    }

    [Fact]
    public async Task Web_scrape_is_used_when_bahamut_misses_and_skips_ai()
    {
        var translator = new RecordingTranslator { Translation = "不該出現" };
        var web = new StubWeb(new CommunityTranslation(
            "網頁譯文一行\n二\n三\n四",
            "Hello Adele 歌詞翻譯",
            "https://www.mojim.com/twy1.htm",
            "Mojim"));
        var pipeline = Create(
            new StubLrclib(new LrclibTrack
            {
                TrackName = "Hello",
                ArtistName = "Adele",
                PlainLyrics = "Hello from the other side",
            }),
            translator,
            apiKey: "sk-test",
            web: web);

        var result = await pipeline.ResolveAsync(Song("Hello", "Adele"), CancellationToken.None);

        Assert.False(translator.WasCalled);
        Assert.Equal(LyricsSource.Web, result.TranslationSource);
        Assert.Equal("社群／LRCLIB → 網頁", result.SourceLabel);
        Assert.Contains("網頁譯文一行", result.Translation);
    }

    [Fact]
    public async Task Cached_ai_is_replaced_when_bahamut_hits_on_replay()
    {
        var cache = new MemoryLyricsCache();
        var query = Song("Hello", "Adele");
        await cache.UpsertAsync(new CachedLyrics
        {
            CacheKey = query.CacheKey,
            Title = query.DisplayTitle,
            Artist = query.DisplayArtist,
            OriginalLyrics = "Hello from the other side",
            OriginalSource = LyricsSource.Lrclib,
            Translation = "舊的 AI 譯文",
            TranslationSource = LyricsSource.Ai,
            SyncedLyrics = "[00:01.00] Hello",
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var translator = new RecordingTranslator { Translation = "又不該出現" };
        var bahamut = new RecordingBahamut(new CommunityTranslation(
            "社群你好\n第二行\n第三行\n第四行",
            "Hello Adele 歌詞翻譯",
            "https://home.gamer.com.tw/artwork.php?sn=9"));
        var pipeline = new LyricsPipeline(
            cache,
            new MissLrclib(),
            bahamut,
            new MissWeb(),
            () => translator,
            () => new AppSettings { ApiKey = "sk-test" });

        var result = await pipeline.ResolveAsync(query, CancellationToken.None);

        Assert.True(bahamut.WasCalled);
        Assert.False(translator.WasCalled);
        Assert.Equal(LyricsSource.Bahamut, result.TranslationSource);
        Assert.Contains("社群你好", result.Translation);
        Assert.Equal("[00:01.00] Hello", result.SyncedLyrics);
    }

    [Fact]
    public async Task Cache_hit_skips_ai_and_keeps_existing_synced()
    {
        var cache = new MemoryLyricsCache();
        var query = Song("Hello", "Adele");
        await cache.UpsertAsync(new CachedLyrics
        {
            CacheKey = query.CacheKey,
            Title = query.DisplayTitle,
            Artist = query.DisplayArtist,
            OriginalLyrics = "Hello",
            OriginalSource = LyricsSource.Lrclib,
            Translation = "你好",
            TranslationSource = LyricsSource.Ai,
            SyncedLyrics = "[00:01.00] Hello",
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var translator = new RecordingTranslator();
        var lrclib = new MissLrclib();
        var pipeline = new LyricsPipeline(cache, lrclib, new MissBahamut(), new MissWeb(), () => translator, () => new AppSettings { ApiKey = "sk-test" });

        var result = await pipeline.ResolveAsync(query, CancellationToken.None);

        Assert.Equal("你好", result.Translation);
        Assert.Equal("[00:01.00] Hello", result.SyncedLyrics);
        Assert.False(translator.WasCalled);
        Assert.False(lrclib.WasCalled);
    }

    [Fact]
    public async Task Cache_hit_without_synced_asks_lrclib_only_for_timestamps()
    {
        var cache = new MemoryLyricsCache();
        var query = Song("Hello", "Adele");
        await cache.UpsertAsync(new CachedLyrics
        {
            CacheKey = query.CacheKey,
            Title = query.DisplayTitle,
            Artist = query.DisplayArtist,
            OriginalLyrics = "Hello",
            OriginalSource = LyricsSource.Lrclib,
            Translation = "你好",
            TranslationSource = LyricsSource.Ai,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var translator = new RecordingTranslator();
        var lrclib = new StubLrclib(new LrclibTrack
        {
            TrackName = "Hello",
            ArtistName = "Adele",
            PlainLyrics = "Hello",
            SyncedLyrics = "[00:05.00] Hello",
        });
        var pipeline = new LyricsPipeline(cache, lrclib, new MissBahamut(), new MissWeb(), () => translator, () => new AppSettings { ApiKey = "sk-test" });

        var result = await pipeline.ResolveAsync(query, CancellationToken.None);

        Assert.Equal("你好", result.Translation);
        Assert.Equal("[00:05.00] Hello", result.SyncedLyrics);
        Assert.False(translator.WasCalled);
    }

    [Fact]
    public async Task Japanese_song_uses_bahamut_translation_and_skips_ai()
    {
        var translator = new RecordingTranslator();
        var bahamut = new StubBahamut(new CommunityTranslation(
            "在夜裡往前衝\n你的眼睛裡\n還有什麼\n越過時間",
            "夜に駆ける-YOASOBI 中日歌詞翻譯",
            "https://home.gamer.com.tw/artwork.php?sn=1"));
        var pipeline = Create(
            new StubLrclib(new LrclibTrack
            {
                TrackName = "夜に駆ける",
                ArtistName = "YOASOBI",
                PlainLyrics = "夜に駆ける\n君の瞳に\nまだ何か\n時を超えて",
                SyncedLyrics = "[00:10.00] 夜に駆ける",
            }),
            translator,
            apiKey: "sk-test",
            bahamut);

        var result = await pipeline.ResolveAsync(Song("夜に駆ける", "YOASOBI"), CancellationToken.None);

        Assert.False(translator.WasCalled);
        Assert.Equal(LyricsSource.Bahamut, result.TranslationSource);
        Assert.Equal("社群／LRCLIB → 巴哈姆特", result.SourceLabel);
        Assert.Contains("在夜裡往前衝", result.Translation);
        Assert.Equal("[00:10.00] 夜に駆ける", result.SyncedLyrics);
    }

    [Fact]
    public async Task Bahamut_timeout_falls_through_to_ai()
    {
        var translator = new RecordingTranslator { Translation = "AI 譯文" };
        var pipeline = Create(
            new StubLrclib(new LrclibTrack
            {
                TrackName = "夜に駆ける",
                ArtistName = "YOASOBI",
                PlainLyrics = "夜に駆ける\n君の瞳に恋をして",
            }),
            translator,
            apiKey: "sk-test",
            new ThrowingBahamut());

        var result = await pipeline.ResolveAsync(Song("夜に駆ける", "YOASOBI"), CancellationToken.None);

        Assert.True(translator.WasCalled);
        Assert.Equal("AI 譯文", result.Translation);
        Assert.Equal("社群／LRCLIB → AI", result.SourceLabel);
    }

    [Fact]
    public async Task Paste_then_translate_is_labeled_hand_paste()
    {
        var translator = new RecordingTranslator { Translation = "手貼譯文" };
        var pipeline = Create(new MissLrclib(), translator, apiKey: "sk-test");
        var result = await pipeline.ApplyPastedOriginalAsync(Song("X", "Y"), "pasted line", CancellationToken.None);

        Assert.Equal(LyricsStatus.Ready, result.Status);
        Assert.Equal("pasted line", result.OriginalLyrics);
        Assert.Equal("手貼 → AI", result.SourceLabel);
    }

    [Fact]
    public async Task Paste_asks_lrclib_for_synced_when_cache_has_none()
    {
        var translator = new RecordingTranslator { Translation = "手貼譯文" };
        var pipeline = Create(
            new StubLrclib(new LrclibTrack
            {
                TrackName = "X",
                ArtistName = "Y",
                SyncedLyrics = "[00:03.00] pasted line",
            }),
            translator,
            apiKey: "sk-test");

        var result = await pipeline.ApplyPastedOriginalAsync(Song("X", "Y"), "pasted line", CancellationToken.None);

        Assert.Equal(LyricsStatus.Ready, result.Status);
        Assert.Equal("[00:03.00] pasted line", result.SyncedLyrics);
        Assert.Equal("手貼譯文", result.Translation);
    }

    [Fact]
    public async Task Missing_api_key_keeps_original_and_asks_for_key()
    {
        var pipeline = Create(
            new StubLrclib(new LrclibTrack { TrackName = "Hello", ArtistName = "Adele", PlainLyrics = "Hello" }),
            new RecordingTranslator(),
            apiKey: null);

        var result = await pipeline.ResolveAsync(Song("Hello", "Adele"), CancellationToken.None);

        Assert.Equal(LyricsStatus.NeedsApiKey, result.Status);
        Assert.Equal("Hello", result.OriginalLyrics);
        Assert.Null(result.Translation);
    }

    [Fact]
    public async Task Traditional_chinese_original_is_not_sent_to_ai()
    {
        var translator = new RecordingTranslator();
        var pipeline = Create(
            new StubLrclib(new LrclibTrack
            {
                TrackName = "歌",
                ArtistName = "人",
                PlainLyrics = "這是繁體歌詞\n我不離開\n你的眼睛為什麼還不說",
            }),
            translator,
            apiKey: "sk-test");

        var result = await pipeline.ResolveAsync(Song("歌", "人"), CancellationToken.None);

        Assert.False(translator.WasCalled);
        Assert.Equal(result.OriginalLyrics, result.Translation);
        Assert.Equal("社群／LRCLIB", result.SourceLabel);
    }

    [Fact]
    public async Task Japanese_lyrics_are_sent_to_ai()
    {
        const string japanese =
            """
            夜に駆ける
            君の瞳に恋をして
            愛してる
            時を超えて
            """;
        var translator = new RecordingTranslator { Translation = "在夜裡奔馳" };
        var pipeline = Create(
            new StubLrclib(new LrclibTrack
            {
                TrackName = "夜に駆ける",
                ArtistName = "YOASOBI",
                PlainLyrics = japanese,
            }),
            translator,
            apiKey: "sk-test");

        var result = await pipeline.ResolveAsync(Song("夜に駆ける", "YOASOBI"), CancellationToken.None);

        Assert.True(translator.WasCalled);
        Assert.Equal(japanese, result.OriginalLyrics);
        Assert.Equal("在夜裡奔馳", result.Translation);
        Assert.Equal("社群／LRCLIB → AI", result.SourceLabel);
    }

    [Fact]
    public async Task Stale_japanese_as_chinese_cache_is_retried_with_ai()
    {
        const string japanese = "愛してる\n時を超えて";
        var cache = new MemoryLyricsCache();
        var query = Song("夜に駆ける", "YOASOBI");
        await cache.UpsertAsync(new CachedLyrics
        {
            CacheKey = query.CacheKey,
            Title = query.DisplayTitle,
            Artist = query.DisplayArtist,
            OriginalLyrics = japanese,
            OriginalSource = LyricsSource.Lrclib,
            Translation = japanese,
            TranslationSource = LyricsSource.Lrclib,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var translator = new RecordingTranslator { Translation = "我愛你" };
        var pipeline = new LyricsPipeline(cache, new MissLrclib(), new MissBahamut(), new MissWeb(), () => translator, () => new AppSettings { ApiKey = "sk-test" });

        var result = await pipeline.ResolveAsync(query, CancellationToken.None);

        Assert.True(translator.WasCalled);
        Assert.Equal("我愛你", result.Translation);
    }

    private static LyricsPipeline Create(
        ILrclibClient lrclib,
        ILyricsTranslator translator,
        string? apiKey,
        IBahamutClient? bahamut = null,
        IWebLyricsClient? web = null) =>
        new(new MemoryLyricsCache(), lrclib, bahamut ?? new MissBahamut(), web ?? new MissWeb(), () => translator, () => new AppSettings { ApiKey = apiKey });

    private static TrackQuery Song(string title, string artist) =>
        TrackNormalizer.FromRaw(title, artist, "THE BOOK", TimeSpan.FromSeconds(200), "Chrome", PlayerKind.Browser, true);

    private sealed class MissLrclib : ILrclibClient
    {
        public bool WasCalled { get; private set; }

        public Task<LrclibTrack?> FindAsync(TrackQuery query, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult<LrclibTrack?>(null);
        }
    }

    private sealed class StubLrclib(LrclibTrack track) : ILrclibClient
    {
        public Task<LrclibTrack?> FindAsync(TrackQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<LrclibTrack?>(track);
    }

    private sealed class MissBahamut : IBahamutClient
    {
        public Task<CommunityTranslation?> FindAsync(TrackQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CommunityTranslation?>(null);
    }

    private sealed class StubBahamut(CommunityTranslation translation) : IBahamutClient
    {
        public Task<CommunityTranslation?> FindAsync(TrackQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CommunityTranslation?>(translation);
    }

    private sealed class ThrowingBahamut : IBahamutClient
    {
        public Task<CommunityTranslation?> FindAsync(TrackQuery query, CancellationToken cancellationToken) =>
            throw new HttpRequestException("timeout");
    }

    private sealed class MissWeb : IWebLyricsClient
    {
        public Task<CommunityTranslation?> FindAsync(TrackQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CommunityTranslation?>(null);
    }

    private sealed class StubWeb(CommunityTranslation translation) : IWebLyricsClient
    {
        public Task<CommunityTranslation?> FindAsync(TrackQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CommunityTranslation?>(translation);
    }

    private sealed class RecordingBahamut(CommunityTranslation translation) : IBahamutClient
    {
        public bool WasCalled { get; private set; }

        public Task<CommunityTranslation?> FindAsync(TrackQuery query, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult<CommunityTranslation?>(translation);
        }
    }

    private sealed class RecordingTranslator : ILyricsTranslator
    {
        public bool WasCalled { get; private set; }
        public TranslationRequest? LastRequest { get; private set; }
        public string Translation { get; init; } = "譯";

        public Task<string> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            LastRequest = request;
            return Task.FromResult(Translation);
        }
    }

    private sealed class MemoryLyricsCache : ILyricsCache
    {
        private readonly Dictionary<string, CachedLyrics> _items = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CachedLyrics?> GetAsync(string cacheKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.TryGetValue(cacheKey, out var item) ? item : null);

        public Task UpsertAsync(CachedLyrics record, CancellationToken cancellationToken = default)
        {
            _items[record.CacheKey] = record;
            return Task.CompletedTask;
        }
    }
}
