using System.Net;
using System.Text;
using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;

namespace LyricsTranslator.Core.Tests;

public class BahamutClientTests
{
    [Fact]
    public async Task Fetches_top_scored_hit_and_extracts_translation()
    {
        var search =
            """
            <a href="creationDetail.php?sn=99">日本遊記</a>
            <a href="creationDetail.php?sn=1">夜に駆ける-YOASOBI 中日歌詞翻譯</a>
            """;
        var article =
            """
            <div id="article_content">
            ▍在夜裡往前衝<br>
            ▍你的眼睛裡<br>
            ▍還有什麼<br>
            ▍越過時間
            </div>
            """;
        var handler = new StubHandler
        {
            Responses =
            {
                ["search.php"] = search,
                ["artwork.php?sn=1"] = article,
            },
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://home.gamer.com.tw/") };
        var client = new BahamutClient(http);
        var query = TrackNormalizer.FromRaw("夜に駆ける", "YOASOBI", null, TimeSpan.FromSeconds(260), "Chrome", PlayerKind.Browser, true);

        var hit = await client.FindAsync(query, CancellationToken.None);

        Assert.NotNull(hit);
        Assert.Contains("在夜裡往前衝", hit!.Translation);
        Assert.Equal("巴哈姆特", hit.SiteLabel);
        Assert.Contains("search.php", handler.Requested, StringComparison.Ordinal);
        Assert.Contains("artwork.php?sn=1", handler.Requested, StringComparison.Ordinal);
        Assert.DoesNotContain("artwork.php?sn=99", handler.Requested, StringComparison.Ordinal);
    }

    [Fact]
    public async Task English_title_still_searches_bahamut()
    {
        var search =
            """
            <a class="TS1" href="artwork.php?sn=9">Hello - Adele 歌詞翻譯</a>
            """;
        var article =
            """
            <div id="article_content">
            ▍你好來自另一邊<br>
            ▍我必須說<br>
            ▍我已試過<br>
            ▍告訴你一切
            </div>
            """;
        var handler = new StubHandler
        {
            Responses =
            {
                ["search.php"] = search,
                ["artwork.php?sn=9"] = article,
            },
        };
        using var http = new HttpClient(handler);
        var client = new BahamutClient(http);
        var query = TrackNormalizer.FromRaw("Hello", "Adele", null, null, "Chrome", PlayerKind.Browser, true);

        var hit = await client.FindAsync(query, CancellationToken.None);

        Assert.NotNull(hit);
        Assert.Contains("你好來自另一邊", hit!.Translation);
        Assert.Contains("search.php", handler.Requested, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("Hello"), handler.Requested, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Http_errors_return_null_instead_of_throwing()
    {
        var handler = new StubHandler { Throw = true };
        using var http = new HttpClient(handler);
        var client = new BahamutClient(http);
        var query = TrackNormalizer.FromRaw("夜に駆ける", "YOASOBI", null, null, "Chrome", PlayerKind.Browser, true);

        var hit = await client.FindAsync(query, CancellationToken.None);

        Assert.Null(hit);
    }

    [Fact]
    public async Task Skips_already_chinese_titles_without_http()
    {
        var handler = new StubHandler();
        using var http = new HttpClient(handler);
        var client = new BahamutClient(http);
        var query = TrackNormalizer.FromRaw("這是繁體歌詞", "歌手", null, null, "Chrome", PlayerKind.Browser, true);

        var hit = await client.FindAsync(query, CancellationToken.None);

        Assert.Null(hit);
        Assert.Equal(string.Empty, handler.Requested);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public Dictionary<string, string> Responses { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool Throw { get; init; }
        public string Requested { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            Requested += url + "\n";
            if (Throw)
            {
                throw new HttpRequestException("offline");
            }

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
