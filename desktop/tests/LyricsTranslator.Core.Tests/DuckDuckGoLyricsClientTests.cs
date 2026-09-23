using System.Net;
using System.Text;
using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;

namespace LyricsTranslator.Core.Tests;

public class DuckDuckGoLyricsClientTests
{
    [Fact]
    public void Parses_allowed_lyric_results_and_skips_unrelated_hosts()
    {
        const string html =
            """
            <a class="result__a" href="https://duckduckgo.com/l/?uddg=https%3A%2F%2Fhome.gamer.com.tw%2Fartwork.php%3Fsn%3D1">Hello Adele 歌詞翻譯</a>
            <a class="result__a" href="https://genius.com/adele-hello-lyrics">Hello lyrics Genius</a>
            <a class="result__a" href="https://www.mojim.com/twy123.htm">Adele Hello 中文歌詞</a>
            """;
        var hits = DuckDuckGoLyricsClient.ParseSearchHits(html);
        Assert.Equal(2, hits.Count);
        Assert.Equal("https://home.gamer.com.tw/artwork.php?sn=1", hits[0].Url.AbsoluteUri);
        Assert.Equal("https://www.mojim.com/twy123.htm", hits[1].Url.AbsoluteUri);
        Assert.DoesNotContain(hits, h => h.Url.Host.Contains("genius", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rank_prefers_lyric_translation_over_unrelated()
    {
        var query = TrackNormalizer.FromRaw("Hello", "Adele", null, TimeSpan.FromSeconds(295), "Chrome", PlayerKind.Browser, true);
        var hits = new[]
        {
            new DuckDuckGoLyricsClient.WebHit("日本遊記 Hello", new Uri("https://home.gamer.com.tw/artwork.php?sn=1")),
            new DuckDuckGoLyricsClient.WebHit("Hello - Adele 歌詞翻譯", new Uri("https://home.gamer.com.tw/artwork.php?sn=2")),
        };

        var ranked = DuckDuckGoLyricsClient.Rank(hits, query).ToList();
        Assert.Single(ranked);
        Assert.Contains("歌詞翻譯", ranked[0].Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Rank_matches_english_apple_title_to_zhongri_bahamut_post()
    {
        var query = BahamutParserTests.AppleSunny();
        var hits = new[]
        {
            new DuckDuckGoLyricsClient.WebHit("日本遊記 Sunny", new Uri("https://home.gamer.com.tw/artwork.php?sn=1")),
            new DuckDuckGoLyricsClient.WebHit(
                "【中日歌詞/中文翻譯】晴る (Sunny)【ヨルシカ/葬送のフリーレン】",
                new Uri(BahamutFixture.SunnyArtworkUrl)),
        };

        var ranked = DuckDuckGoLyricsClient.Rank(hits, query).ToList();
        Assert.Single(ranked);
        Assert.Equal(BahamutFixture.SunnyArtworkUrl, ranked[0].Url.AbsoluteUri);
    }

    [Fact]
    public void Rank_matches_aoi_shiori_romaji_to_jp_romaji_zh_post()
    {
        var query = BahamutParserTests.YoutubeAoiShiori();
        var hits = new[]
        {
            new DuckDuckGoLyricsClient.WebHit("日本遊記 Aoi", new Uri("https://home.gamer.com.tw/artwork.php?sn=1")),
            new DuckDuckGoLyricsClient.WebHit(
                "青い栞- Galileo Galilei 日+羅+中 歌詞",
                new Uri(BahamutFixture.AoiShioriArtworkUrl)),
        };

        var ranked = DuckDuckGoLyricsClient.Rank(hits, query, "Aoi Shiori 歌詞").ToList();
        Assert.Single(ranked);
        Assert.Equal(BahamutFixture.AoiShioriArtworkUrl, ranked[0].Url.AbsoluteUri);
    }

    [Fact]
    public async Task Fetches_translation_from_allowlisted_page()
    {
        var search =
            """
            <a class="result__a" href="https://www.mojim.com/twy1.htm">Hello Adele 歌詞翻譯</a>
            """;
        var page =
            """
            <div>
            你好來自另一邊<br>
            我必須跟你說<br>
            我已經試過了<br>
            要告訴你一切
            </div>
            """;
        var handler = new StubHandler
        {
            Responses =
            {
                ["html.duckduckgo.com"] = search,
                ["mojim.com"] = page,
            },
        };
        using var http = new HttpClient(handler);
        var client = new DuckDuckGoLyricsClient(http);
        var query = TrackNormalizer.FromRaw("Hello", "Adele", null, null, "Chrome", PlayerKind.Browser, true);

        var hit = await client.FindAsync(query, CancellationToken.None);

        Assert.NotNull(hit);
        Assert.Contains("你好來自另一邊", hit!.Translation);
        Assert.Equal("Mojim", hit.SiteLabel);
        Assert.False(handler.Requested.Contains("genius", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public Dictionary<string, string> Responses { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string Requested { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            Requested += url + "\n";
            foreach (var (key, html) in Responses)
            {
                if (url.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(html, Encoding.UTF8, "text/html"),
                    });
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
