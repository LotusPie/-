using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;

namespace LyricsTranslator.Core.Tests;

public class BahamutParserTests
{
    [Fact]
    public void Should_search_japanese_titles_but_not_korean_or_english()
    {
        Assert.True(BahamutParser.ShouldSearch(Song("夜に駆ける", "YOASOBI")));
        Assert.True(BahamutParser.ShouldSearch(Song("夜に駆ける (Official Video)", "YOASOBI")));
        Assert.False(BahamutParser.ShouldSearch(Song("Dynamite", "BTS")));
        Assert.False(BahamutParser.ShouldSearch(Song("Hello", "Adele")));
    }

    [Fact]
    public void Parses_creation_hits_and_prefers_lyric_translations()
    {
        const string html =
            """
            <a href="creationDetail.php?sn=5230344">夜に駆ける-YOASOBI 中日歌詞翻譯</a>
            <a href="creationDetail.php?sn=99">日本遊記 2024</a>
            <a href="creationDetail.php?sn=88">2024 Billboard 排行</a>
            <a href="creationDetail.php?sn=77">夜に駆ける 填詞練習</a>
            <a href="creationDetail.php?sn=66">別的歌 歌詞翻譯</a>
            """;
        var query = Song("夜に駆ける", "YOASOBI");
        var hits = BahamutParser.ParseSearchHits(html);
        Assert.Equal(5, hits.Count);

        var ranked = hits
            .Select(h => (Hit: h, Score: BahamutParser.ScoreHit(h, query)))
            .OrderByDescending(x => x.Score)
            .ToList();

        Assert.Equal("5230344", ranked[0].Hit.Sn);
        Assert.True(ranked[0].Score > 100);
        Assert.Equal(0, BahamutParser.ScoreHit(hits.First(h => h.Sn == "99"), query));
        Assert.Equal(0, BahamutParser.ScoreHit(hits.First(h => h.Sn == "88"), query));
        Assert.Equal(0, BahamutParser.ScoreHit(hits.First(h => h.Sn == "66"), query));
        Assert.True(BahamutParser.ScoreHit(hits.First(h => h.Sn == "77"), query) < ranked[0].Score);
    }

    [Fact]
    public void Compact_match_ignores_spaces_and_quotes_on_japanese_titles()
    {
        var query = Song("夜 に 駆ける", "YOASOBI");
        var hit = new BahamutSearchHit("1", "「夜に駆ける」-YOASOBI 歌詞翻譯", "https://example.test/1");
        Assert.True(BahamutParser.ScoreHit(hit, query) >= 100);
    }

    [Fact]
    public void Build_search_url_targets_bahamut_creation_search()
    {
        var url = BahamutParser.BuildSearchUrl(Song("夜に駆ける", "YOASOBI"));
        Assert.StartsWith("https://home.gamer.com.tw/search.php?kw=", url, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("夜に駆ける"), url, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("歌詞翻譯"), url, StringComparison.Ordinal);
    }

    [Fact]
    public void Extracts_traditional_chinese_after_marker_and_skips_meta()
    {
        const string html =
            """
            <div id="article_content">
            作詞：Ayase<br>
            夜に駆ける<br>
            Yoru ni kakeru<br>
            ▍在夜裡往前衝（修正版）<br>
            君の瞳に<br>
            ▍你的眼睛裡<br>
            まだ何か<br>
            ▍還有什麼<br>
            時を超えて<br>
            ▍越過時間<br>
            轉載請註明
            </div>
            <div id="commentRow"></div>
            """;
        var text = BahamutParser.ExtractArticleText(html);
        var lyrics = BahamutParser.ExtractTraditionalChineseLyrics(text);
        Assert.Equal("在夜裡往前衝\n你的眼睛裡\n還有什麼\n越過時間", lyrics);
        Assert.DoesNotContain("作詞", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("夜に駆ける", lyrics, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_returns_null_when_too_few_chinese_lines()
    {
        var lyrics = BahamutParser.ExtractTraditionalChineseLyrics("▍只有一行\n▍兩行");
        Assert.Null(lyrics);
    }

    private static TrackQuery Song(string title, string artist) =>
        TrackNormalizer.FromRaw(title, artist, null, TimeSpan.FromSeconds(260), "Chrome", PlayerKind.Browser, true);
}
