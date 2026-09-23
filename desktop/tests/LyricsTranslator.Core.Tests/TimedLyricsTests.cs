using System.Net;
using System.Text;
using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;

namespace LyricsTranslator.Core.Tests;

public class TimedLyricsTests
{
    [Fact]
    public void Detects_japanese_from_native_script_not_romaji_smtc()
    {
        Assert.Equal(LyricLanguage.Japanese, LyricLanguageDetector.Detect(Song("花一匁", "BURNOUT SYNDROMES")));
        Assert.Equal(LyricLanguage.Japanese, LyricLanguageDetector.Detect(Song("Hanaichi Monnme", "BURNOUT SYNDROMES")));
        Assert.Equal(LyricLanguage.Japanese, LyricLanguageDetector.Detect(Song("あぶく", "YORUSHIKA")));
        Assert.Equal(LyricLanguage.Japanese, LyricLanguageDetector.Detect(BahamutParserTests.YoutubeAoiShiori()));
        Assert.Equal(LyricLanguage.Japanese, LyricLanguageDetector.Detect(BahamutParserTests.AppleSunny()));
    }

    [Fact]
    public void Detects_english_and_korean_from_original_script()
    {
        Assert.Equal(LyricLanguage.English, LyricLanguageDetector.Detect(Song("Hello", "Adele")));
        Assert.Equal(LyricLanguage.Korean, LyricLanguageDetector.Detect(Song("봄날", "BTS")));
    }

    [Fact]
    public void Japanese_netease_queries_use_native_title_not_romaji()
    {
        var queries = NeteaseTimedLyricsClient.BuildSearchQueries(Song("Hanaichi Monnme", "BURNOUT SYNDROMES"));
        Assert.Equal("花一匁", queries[0]);
        Assert.Contains("花一匁 BURNOUT SYNDROMES", queries);

        var abuku = NeteaseTimedLyricsClient.BuildSearchQueries(Song("あぶく", "YORUSHIKA"));
        Assert.Equal("あぶく", abuku[0]);
        Assert.Contains("あぶく ヨルシカ", abuku);

        var aoi = NeteaseTimedLyricsClient.BuildSearchQueries(BahamutParserTests.YoutubeAoiShiori());
        Assert.Contains("青い栞", aoi);
        Assert.Equal("青い栞", aoi[0]);
    }

    [Fact]
    public async Task Netease_fetches_japanese_lrc_and_ignores_tlyric()
    {
        const string search =
            """
            {"result":{"songs":[{"id":11,"name":"あぶく","duration":235000,"artists":[{"name":"ヨルシカ"}]}]}}
            """;
        const string lyric =
            """
            {"lrc":{"lyric":"[00:00.000] 作词 : n-buna\n[00:11.64]あぁどうしようもないほどに\n[00:14.40]私に蠢く獣\n[00:18.10]水面浮かんで浮かんでは消えるあぶく\n[00:29.24]あぁどうしようもなく悲しい"},"tlyric":{"lyric":"[00:11.64]啊啊 简体翻译\n[00:14.40]不是繁中"}}
            """;
        var handler = new MapHandler(url =>
        {
            if (url.Contains("api/search/get", StringComparison.Ordinal))
            {
                Assert.True(
                    url.Contains(Uri.EscapeDataString("あぶく"), StringComparison.Ordinal) ||
                    url.Contains("あぶく", StringComparison.Ordinal));
                return search;
            }

            if (url.Contains("api/song/lyric", StringComparison.Ordinal) && url.Contains("id=11", StringComparison.Ordinal))
            {
                return lyric;
            }

            return null;
        });
        using var http = new HttpClient(handler);
        var client = new NeteaseTimedLyricsClient(http);

        var hit = await client.FindAsync(Song("あぶく", "YORUSHIKA"), LyricLanguage.Japanese, CancellationToken.None);

        Assert.NotNull(hit);
        Assert.Equal("NetEase", hit!.SourceName);
        Assert.Contains("あぁどうしようもないほどに", hit.SyncedLyrics);
        Assert.DoesNotContain("简体翻译", hit.SyncedLyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("作词", hit.SyncedLyrics, StringComparison.Ordinal);
        Assert.True(LrcLanguageFilter.HasTimestamps(hit.SyncedLyrics));
    }

    [Fact]
    public async Task Netease_skips_english_language()
    {
        var handler = new MapHandler(_ => throw new InvalidOperationException("JP-only path must not run"));
        using var http = new HttpClient(handler);
        var client = new NeteaseTimedLyricsClient(http);
        var hit = await client.FindAsync(Song("Hello", "Adele"), LyricLanguage.English, CancellationToken.None);
        Assert.Null(hit);
        Assert.Equal(string.Empty, handler.Requested);
    }

    [Fact]
    public async Task Netease_rejects_lyrics_without_timestamps()
    {
        const string search =
            """
            {"result":{"songs":[{"id":9,"name":"花一匁","duration":275000,"artists":[{"name":"BURNOUT SYNDROMES"}]}]}}
            """;
        const string lyric = """{"lrc":{"lyric":"花一匁\nこれは歌詞です\nタイムスタンプなし"}}""";
        var handler = new MapHandler(url =>
            url.Contains("search", StringComparison.Ordinal) ? search :
            url.Contains("lyric", StringComparison.Ordinal) ? lyric : null);
        using var http = new HttpClient(handler);
        var client = new NeteaseTimedLyricsClient(http);
        var hit = await client.FindAsync(Song("花一匁", "BURNOUT SYNDROMES"), LyricLanguage.Japanese, CancellationToken.None);
        Assert.Null(hit);
    }

    [Fact]
    public async Task Router_uses_netease_before_lrclib_for_japanese()
    {
        var netease = new RecordingTimed("NetEase", new TimedLyricsHit(
            "[00:01.00]花一匁だよ\n[00:05.00]正しい日本語\n[00:09.00]まだ仮名がある",
            "NetEase",
            "花一匁",
            "BURNOUT SYNDROMES",
            275));
        var lrclib = new RecordingTimed("LRCLIB", new TimedLyricsHit(
            "[00:01.00]romaji dump\n[00:02.00]should not win\n[00:03.00]nope",
            "LRCLIB",
            "Hanaichi Monnme",
            "BURNOUT SYNDROMES",
            275));
        var router = new TimedLyricsRouter(netease, lrclib);

        var hit = await router.FindAsync(Song("Hanaichi Monnme", "BURNOUT SYNDROMES"), CancellationToken.None);

        Assert.Equal("NetEase", hit!.SourceName);
        Assert.Contains("正しい日本語", hit.SyncedLyrics);
        Assert.True(netease.WasCalled);
        Assert.False(lrclib.WasCalled);
        Assert.Equal(["NetEase", "LRCLIB"], router.SourcesFor(LyricLanguage.Japanese));
    }

    [Fact]
    public async Task Router_does_not_use_jp_netease_path_for_english()
    {
        var netease = new RecordingTimed("NetEase", new TimedLyricsHit(
            "[00:01.00]夜に駆ける\n[00:02.00]日本語の歌\n[00:03.00]だめだよ",
            "NetEase",
            "Hello",
            "Adele",
            295));
        var lrclib = new RecordingTimed("LRCLIB", new TimedLyricsHit(
            "[00:01.00]Hello from the other side\n[00:02.00]I must have called\n[00:03.00]a thousand times",
            "LRCLIB",
            "Hello",
            "Adele",
            295));
        var router = new TimedLyricsRouter(netease, lrclib);

        var hit = await router.FindAsync(Song("Hello", "Adele"), CancellationToken.None);

        Assert.False(netease.WasCalled);
        Assert.True(lrclib.WasCalled);
        Assert.Equal("LRCLIB", hit!.SourceName);
        Assert.Equal(["LRCLIB"], router.SourcesFor(LyricLanguage.English));
    }

    [Fact]
    public void Rank_timed_skips_romaji_lrclib_when_japanese_original_is_known()
    {
        var query = Song("花一匁", "BURNOUT SYNDROMES");
        var romaji = new LrclibTrack
        {
            TrackName = "Hanaichi Monnme",
            ArtistName = "BURNOUT SYNDROMES",
            Duration = 275,
            SyncedLyrics = "[00:01.00]hanaichi\n[00:02.00]monnme\n[00:03.00]romaji only",
        };
        var japanese = new LrclibTrack
        {
            TrackName = "花一匁",
            ArtistName = "BURNOUT SYNDROMES",
            Duration = 275,
            SyncedLyrics = "[00:01.00]花一匁だよ\n[00:02.00]正しい歌詞\n[00:03.00]まだ仮名",
        };

        var timed = LrclibClient.RankTimed([romaji, japanese], query).ToList();
        Assert.Single(timed);
        Assert.Equal("花一匁", timed[0].TrackName);
        Assert.DoesNotContain(timed, t => string.Equals(t.TrackName, "Hanaichi Monnme", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Language_aware_client_prefers_jp_timed_lrc_over_romaji_dump()
    {
        var lrclib = new StubCatalog(new LrclibTrack
        {
            Id = 8,
            TrackName = "Aoi Shiori",
            ArtistName = "Galileo Galilei",
            PlainLyrics = "何ページもついやして",
            SyncedLyrics = "[00:01.00]Nan PAGE mo\n[00:02.00]tsuiyashite\n[00:03.00]romaji",
        });
        var netease = new RecordingTimed("NetEase", new TimedLyricsHit(
            "[00:01.00]何ページもついやして\n[00:02.00]綴られた僕らの気分\n[00:03.00]どうしてか一行の",
            "NetEase",
            "青い栞",
            "Galileo Galilei",
            337));
        var router = new TimedLyricsRouter(netease, new RecordingTimed("LRCLIB", null));
        var client = new LanguageAwareLrclibClient(lrclib, router);

        var hit = await client.FindAsync(BahamutParserTests.YoutubeAoiShiori(), CancellationToken.None);

        Assert.NotNull(hit);
        Assert.Equal("何ページもついやして", hit!.PlainLyrics);
        Assert.Contains("綴られた僕らの気分", hit.SyncedLyrics);
        Assert.DoesNotContain("Nan PAGE", hit.SyncedLyrics, StringComparison.OrdinalIgnoreCase);
        Assert.True(netease.WasCalled);
    }

    [Fact]
    public void Filter_rejects_plain_text_without_timestamps()
    {
        Assert.False(LrcLanguageFilter.HasTimestamps("ただの歌詞\nタイムスタンプなし\nまだない"));
        Assert.False(LrcLanguageFilter.Fits("just words\nno stamps\nstill none", LyricLanguage.English));
    }

    private static TrackQuery Song(string title, string artist) =>
        TrackNormalizer.FromRaw(title, artist, null, TimeSpan.FromSeconds(260), "Chrome", PlayerKind.Browser, true);

    private sealed class RecordingTimed(string name, TimedLyricsHit? hit) : ITimedLyricsSource
    {
        public string Name { get; } = name;
        public bool WasCalled { get; private set; }

        public Task<TimedLyricsHit?> FindAsync(TrackQuery query, LyricLanguage language, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(hit);
        }
    }

    private sealed class StubCatalog(LrclibTrack track) : ILrclibClient
    {
        public Task<LrclibTrack?> FindAsync(TrackQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<LrclibTrack?>(track);
    }

    private sealed class MapHandler(Func<string, string?> bodyForUrl) : HttpMessageHandler
    {
        public string Requested { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            Requested += url + "\n";
            var body = bodyForUrl(url);
            if (body is null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
