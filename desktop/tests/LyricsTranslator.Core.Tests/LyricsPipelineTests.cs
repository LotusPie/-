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
            }),
            translator,
            apiKey: "sk-test");

        var result = await pipeline.ResolveAsync(Song("Hello", "Adele"), CancellationToken.None);

        Assert.Equal(LyricsStatus.Ready, result.Status);
        Assert.Equal("Hello from the other side", result.OriginalLyrics);
        Assert.Equal("繁中一行", result.Translation);
        Assert.Equal("社群／LRCLIB → AI", result.SourceLabel);
        Assert.True(translator.WasCalled);
    }

    [Fact]
    public async Task Cache_hit_skips_network_and_ai()
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
        var lrclib = new MissLrclib();
        var pipeline = new LyricsPipeline(cache, lrclib, () => translator, () => new AppSettings { ApiKey = "sk-test" });

        var result = await pipeline.ResolveAsync(query, CancellationToken.None);

        Assert.Equal("你好", result.Translation);
        Assert.False(translator.WasCalled);
        Assert.False(lrclib.WasCalled);
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
        var pipeline = new LyricsPipeline(cache, new MissLrclib(), () => translator, () => new AppSettings { ApiKey = "sk-test" });

        var result = await pipeline.ResolveAsync(query, CancellationToken.None);

        Assert.True(translator.WasCalled);
        Assert.Equal("我愛你", result.Translation);
    }

    private static LyricsPipeline Create(ILrclibClient lrclib, ILyricsTranslator translator, string? apiKey) =>
        new(new MemoryLyricsCache(), lrclib, () => translator, () => new AppSettings { ApiKey = apiKey });

    private static TrackQuery Song(string title, string artist) =>
        TrackNormalizer.FromRaw(title, artist, null, TimeSpan.FromSeconds(200), "Chrome", PlayerKind.Browser, true);

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

    private sealed class RecordingTranslator : ILyricsTranslator
    {
        public bool WasCalled { get; private set; }
        public string Translation { get; init; } = "譯";

        public Task<string> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
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
