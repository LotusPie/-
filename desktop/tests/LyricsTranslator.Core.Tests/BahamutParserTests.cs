using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;

namespace LyricsTranslator.Core.Tests;

public class BahamutParserTests
{
    [Fact]
    public void Should_search_foreign_titles_including_english_and_korean()
    {
        Assert.True(BahamutParser.ShouldSearch(Song("夜に駆ける", "YOASOBI")));
        Assert.True(BahamutParser.ShouldSearch(Song("夜に駆ける (Official Video)", "YOASOBI")));
        Assert.True(BahamutParser.ShouldSearch(Song("Dynamite", "BTS")));
        Assert.True(BahamutParser.ShouldSearch(Song("Hello", "Adele")));
        Assert.False(BahamutParser.ShouldSearch(Song("這是繁體歌詞", "歌手")));
    }

    [Fact]
    public void Build_search_queries_cover_how_a_person_would_google()
    {
        var queries = BahamutParser.BuildSearchQueries(Song("夜に駆ける", "YOASOBI"));
        Assert.Equal(
            [
                "夜に駆ける",
                "夜に駆ける YOASOBI",
                "夜に駆ける 歌詞",
                "夜に駆ける 歌詞翻譯",
                "夜に駆ける 中文歌詞",
                "夜に駆ける YOASOBI 歌詞",
                "夜に駆ける YOASOBI 歌詞翻譯",
            ],
            queries);
    }

    [Fact]
    public void Parses_creation_and_artwork_hits_and_prefers_lyric_translations()
    {
        const string html =
            """
            <a href="creationDetail.php?sn=5230344">夜に駆ける-YOASOBI 中日歌詞翻譯</a>
            <a class="TS1" href="artwork.php?sn=5194612">【歌詞翻譯】YOASOBI《夜に駆ける》中文歌詞</a>
            <a href="creationDetail.php?sn=99">日本遊記 2024</a>
            <a href="creationDetail.php?sn=88">2024 Billboard 排行</a>
            <a href="creationDetail.php?sn=77">夜に駆ける 填詞練習</a>
            <a href="creationDetail.php?sn=66">別的歌 歌詞翻譯</a>
            <a href="creationDetail.php?sn=55">榎宮月的自我介紹</a>
            """;
        var query = Song("夜に駆ける", "YOASOBI");
        var hits = BahamutParser.ParseSearchHits(html);
        Assert.Contains(hits, h => h.Sn == "5230344");
        Assert.Contains(hits, h => h.Sn == "5194612" && h.Url.Contains("artwork.php", StringComparison.Ordinal));

        var ranked = hits
            .Select(h => (Hit: h, Score: BahamutParser.ScoreHit(h, query)))
            .OrderByDescending(x => x.Score)
            .ToList();

        Assert.Contains("歌詞", ranked[0].Hit.Title, StringComparison.Ordinal);
        Assert.True(ranked[0].Score > 100);
        Assert.Equal(0, BahamutParser.ScoreHit(hits.First(h => h.Sn == "99"), query));
        Assert.Equal(0, BahamutParser.ScoreHit(hits.First(h => h.Sn == "88"), query));
        Assert.Equal(0, BahamutParser.ScoreHit(hits.First(h => h.Sn == "66"), query));
        Assert.Equal(0, BahamutParser.ScoreHit(new BahamutSearchHit("55", "榎宮月的自我介紹", "u"), query));
        Assert.True(BahamutParser.ScoreHit(hits.First(h => h.Sn == "77"), query) < ranked[0].Score);
    }

    [Fact]
    public void Short_english_title_without_artist_is_not_a_hit()
    {
        var query = Song("Hello", "Adele");
        var unrelated = new BahamutSearchHit("1", "HELLO WORLD - LiSA 中日歌詞翻譯", "https://example.test/1");
        var matched = new BahamutSearchHit("2", "Hello - Adele 歌詞翻譯", "https://example.test/2");
        Assert.Equal(0, BahamutParser.ScoreHit(unrelated, query));
        Assert.True(BahamutParser.ScoreHit(matched, query) > 100);
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
    public void Extracts_chinese_from_list_items_and_bracket_tags()
    {
        const string html =
            """
            <div id="article_content">
            <font color="#000080">沈むように溶けてゆくように</font>
            <ul><li>就好像沉溺般融化一般</li><li>在寬廣無際的夜裡只有兩人的天空</li></ul>
            【中】就只是再見一句
            【譯】僅憑一句話就理解到了
            </div>
            """;
        var text = BahamutParser.ExtractArticleText(html);
        var lyrics = BahamutParser.ExtractTraditionalChineseLyrics(text);
        Assert.Contains("就好像沉溺般融化一般", lyrics);
        Assert.Contains("在寬廣無際的夜裡只有兩人的天空", lyrics);
        Assert.Contains("就只是再見一句", lyrics);
        Assert.Contains("僅憑一句話就理解到了", lyrics);
        Assert.DoesNotContain("沈む", lyrics, StringComparison.Ordinal);
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
