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
        Assert.Contains("夜に駆ける", queries);
        Assert.Contains("夜に駆ける YOASOBI", queries);
        Assert.Contains("夜に駆ける 歌詞", queries);
        Assert.Contains("夜に駆ける 歌詞翻譯", queries);
        Assert.Contains("夜に駆ける 中文歌詞", queries);
        Assert.Contains("夜に駆ける 中日歌詞", queries);
        Assert.Contains("夜に駆ける YOASOBI 歌詞", queries);
        Assert.Contains("夜に駆ける YOASOBI 歌詞翻譯", queries);
        Assert.True(queries.Count <= BahamutParser.MaxSearchQueries);
    }

    [Fact]
    public void Build_search_queries_for_english_apple_title_include_native_aliases()
    {
        var queries = BahamutParser.BuildSearchQueries(AppleSunny()).ToList();
        Assert.Contains("Sunny 歌詞", queries);
        Assert.Contains("晴る 歌詞", queries);
        Assert.Contains("晴る ヨルシカ 歌詞翻譯", queries);
        Assert.Contains("Sunny 中日歌詞", queries);
        Assert.Contains("Sunny", queries);
        Assert.True(queries.Count <= BahamutParser.MaxSearchQueries);
        Assert.Contains(queries, q => q.Contains("ヨルシカ", StringComparison.Ordinal));
        Assert.True(
            queries.FindIndex(q => q.Contains("晴る", StringComparison.Ordinal)) >= 0 &&
            queries.FindIndex(q => q.Contains("晴る", StringComparison.Ordinal)) <=
            queries.FindIndex(q => q.Contains("Sunny", StringComparison.Ordinal)));
    }

    [Fact]
    public void Prefers_japanese_hanaichi_over_apple_music_romaji()
    {
        var fromJapanese = BahamutParser.BuildSearchQueries(Song("花一匁", "BURNOUT SYNDROMES"));
        Assert.Equal("花一匁", fromJapanese[0]);
        Assert.Contains("花一匁 歌詞", fromJapanese);
        Assert.True(fromJapanese.Take(6).All(q => !q.Contains("Hanaichi", StringComparison.OrdinalIgnoreCase)));

        var fromRomaji = BahamutParser.BuildSearchQueries(Song("Hanaichi Monnme", "BURNOUT SYNDROMES")).ToList();
        Assert.Equal("花一匁", fromRomaji[0]);
        Assert.Contains("花一匁 歌詞", fromRomaji);
        var nativeAt = fromRomaji.FindIndex(q => q.Contains("花一匁", StringComparison.Ordinal));
        var romajiAt = fromRomaji.FindIndex(q => q.Contains("Hanaichi", StringComparison.OrdinalIgnoreCase));
        Assert.True(nativeAt >= 0);
        Assert.True(romajiAt < 0 || nativeAt < romajiAt);
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
        Assert.Equal(0, BahamutParser.ScoreHit(unrelated, query, "Hello 歌詞"));
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

    [Fact]
    public void Extracts_traditional_chinese_from_same_line_jp_zh_pairs()
    {
        const string text =
            """
            貴方は風のように / 你就像微風一般
            目を閉じては夕暮れ／闔上雙眼染上暮色
            何を思っているんだろうか / 究竟你內心在想甚麼呢
            目蓋を開いていた／你睜開的眼臉底下
            """;
        var lyrics = BahamutParser.ExtractTraditionalChineseLyrics(text);
        Assert.Equal("你就像微風一般\n闔上雙眼染上暮色\n究竟你內心在想甚麼呢\n你睜開的眼臉底下", lyrics);
        Assert.DoesNotContain("貴方", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("目を", lyrics, StringComparison.Ordinal);
    }

    [Fact]
    public void Scores_sunny_yorushika_against_haru_zhongri_post()
    {
        var query = AppleSunny();
        var hit = new BahamutSearchHit(
            BahamutFixture.SunnySn,
            "【中日歌詞/中文翻譯】晴る(Sunny) 【ヨルシカ/葬送のフリーレン】",
            BahamutFixture.SunnyArtworkUrl);
        Assert.True(BahamutParser.ScoreHit(hit, query) > 100);

        var nativeOnly = new BahamutSearchHit(
            "2",
            "【中日歌詞】晴る",
            "https://home.gamer.com.tw/artwork.php?sn=2");
        Assert.True(BahamutParser.ScoreHit(nativeOnly, query) > 100);
    }

    [Fact]
    public void Fixture_html_extracts_interleaved_jp_then_zh_without_kana()
    {
        var html = BahamutFixture.ReadSunnyArtwork();
        var hits = BahamutParser.ParseSearchHits(html);
        Assert.Contains(hits, h => h.Sn == BahamutFixture.SunnySn);
        Assert.True(BahamutParser.ScoreHit(hits.First(h => h.Sn == BahamutFixture.SunnySn), AppleSunny()) > 100);

        var lyrics = BahamutParser.ExtractTraditionalChineseLyrics(BahamutParser.ExtractArticleText(html));
        Assert.NotNull(lyrics);
        Assert.Contains("你就像微風一般", lyrics);
        Assert.Contains("闔上雙眼染上暮色", lyrics);
        Assert.DoesNotContain("貴方は", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("目を閉じて", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("歌詞翻譯", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("作詞", lyrics, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_rejects_page_chrome_translator_notes_and_html_residue()
    {
        const string html =
            """
            <div id="article_content" class="text-paragraph article_container">
            中文不是我翻譯的 只是把中日羅的歌詞整理起來而已<br>
            中文翻譯來源：http://b23.tv/ZCxGRfk<br>
            貴方は風のように<br>
            你就像微風一般<br>
            目を閉じては夕暮れ<br>
            闔上雙眼染上暮色<br>
            何を思っているんだろうか<br>
            究竟你內心在想甚麼呢<br>
            目蓋を開いていた<br>
            你睜開的眼臉底下<br>
            「でも、きっと幻？」<br>
            <div class="ct-btn-box"><a>上一篇</a><a>下一篇</a></div>
            <a>留言</a>
            </div>
            """;
        var text = BahamutParser.ExtractArticleText(html);
        Assert.DoesNotContain("article_content", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("text-paragraph", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("上一篇", text, StringComparison.Ordinal);
        Assert.DoesNotContain("下一篇", text, StringComparison.Ordinal);
        Assert.DoesNotContain("留言", text, StringComparison.Ordinal);

        var lyrics = BahamutParser.ExtractTraditionalChineseLyrics(text);
        Assert.NotNull(lyrics);
        Assert.Contains("你就像微風一般", lyrics);
        Assert.Contains("闔上雙眼染上暮色", lyrics);
        Assert.DoesNotContain("上一篇", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("下一篇", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("留言", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("article_content", lyrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("b23.tv", lyrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("不是我翻譯", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("整理起來", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("中文翻譯來源", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", lyrics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Live_or_fixture_sn_5859521_extracts_traditional_chinese()
    {
        var html = await BahamutFixture.LoadSunnyArtworkAsync();
        var lyrics = BahamutParser.ExtractTraditionalChineseLyrics(BahamutParser.ExtractArticleText(html));
        Assert.NotNull(lyrics);
        Assert.Contains("你就像微風一般", lyrics);
        Assert.Contains("闔上雙眼染上暮色", lyrics);
        Assert.DoesNotContain("貴方は風のように", lyrics, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_search_queries_for_aoi_shiori_include_romaji_and_native()
    {
        var queries = BahamutParser.BuildSearchQueries(YoutubeAoiShiori()).ToList();
        Assert.Contains("Aoi Shiori 歌詞", queries);
        Assert.Contains("青い栞 歌詞", queries);
        Assert.Contains("Aoi Shiori Galileo Galilei 歌詞翻譯", queries);
        Assert.Contains("青い栞 中日歌詞", queries);
        Assert.True(queries.Count <= BahamutParser.MaxSearchQueries);
        Assert.True(queries.FindIndex(q => q.Contains("青い栞", StringComparison.Ordinal)) <
                    queries.FindIndex(q => q.Contains("Aoi Shiori", StringComparison.Ordinal)));
    }

    [Fact]
    public void Scores_aoi_shiori_against_jp_romaji_zh_bahamut_post()
    {
        var query = YoutubeAoiShiori();
        var hit = new BahamutSearchHit(
            BahamutFixture.AoiShioriSn,
            "青い栞- Galileo Galilei 日+羅+中 歌詞",
            BahamutFixture.AoiShioriArtworkUrl);
        Assert.True(BahamutParser.ScoreHit(hit, query) > 100);
        Assert.True(BahamutParser.ScoreHit(hit, query, "Aoi Shiori 歌詞") > 100);
    }

    [Fact]
    public void Romaji_playing_title_scores_jp_romaji_zh_hit_from_search_keyword()
    {
        var query = Song("Kimi no Shiranai Monogatari", "Supercell");
        var hit = new BahamutSearchHit(
            "1",
            "君の知らない物語- supercell 日+羅+中歌詞",
            "https://home.gamer.com.tw/artwork.php?sn=1");
        Assert.Equal(0, BahamutParser.ScoreHit(hit, query));
        Assert.True(BahamutParser.ScoreHit(hit, query, "Kimi no Shiranai Monogatari 歌詞") > 100);
        Assert.True(BahamutParser.ScoreHit(hit, query, "Kimi no Shiranai Monogatari Supercell 歌詞翻譯") > 100);
    }

    [Fact]
    public void Fixture_html_extracts_jp_romaji_zh_aoi_shiori()
    {
        var html = BahamutFixture.ReadAoiShioriArtwork();
        var lyrics = BahamutParser.ExtractTraditionalChineseLyrics(BahamutParser.ExtractArticleText(html));
        Assert.NotNull(lyrics);
        Assert.Contains("不管要用掉多少頁", lyrics);
        Assert.Contains("都只想讓我們的心情得以描述", lyrics);
        Assert.DoesNotContain("Nan PAGE", lyrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("何ページ", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("請見諒", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("上一篇", lyrics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Live_or_fixture_sn_3854760_extracts_traditional_chinese()
    {
        var html = await BahamutFixture.LoadAoiShioriArtworkAsync();
        var lyrics = BahamutParser.ExtractTraditionalChineseLyrics(BahamutParser.ExtractArticleText(html));
        Assert.NotNull(lyrics);
        Assert.Contains("不管要用掉多少頁", lyrics);
        Assert.DoesNotContain("tsuiyashite", lyrics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Drops_translator_preface_footnotes_and_keeps_abuku_lyric_lines()
    {
        const string text =
            """
            這首歌是ヨルシカ為動畫寫的OP
            只是經常聽到他們的歌，充其量也只能算路人粉？總之，我邊翻也有邊找一些資料，
            但可能還是有瞭解不夠而翻譯不到位的地方，還請多多包涵囉。
            由於這首歌有很多隱喻和抽象的手法，
            部分需要配合原文深入解釋的地方，我會標橘字並在後面放上詳細註釋。
            最後也會附上我對這首歌的小小理解～
            あぁどうしようもないほどに 私に蠢く獣
            水面浮かんで浮かんでは消える
            あぶく
            啊啊 無可救藥地 在我心底蠢蠢欲動的野獸
            接連浮上水面又無疾而終的
            氣泡
            あぁどうしようもなく悲しい 私を動かす獣
            同義反覆 握緊的手裡留下氣泡¹
            【註釋】
            1.同義反覆 握緊的手裡留下氣泡：トートロジー，又稱套套邏輯。這是整首歌的核心概念。
            回到歌詞，我覺得這裡不是空虛。
            【個人感想】
            暫且不談這首歌在作品當中的寓意，我覺得這是一首側寫出創作者心境的歌。
            """;
        var lyrics = BahamutParser.ExtractTraditionalChineseLyrics(text);
        Assert.NotNull(lyrics);
        Assert.Contains("啊啊 無可救藥地 在我心底蠢蠢欲動的野獸", lyrics);
        Assert.Contains("接連浮上水面又無疾而終的", lyrics);
        Assert.Contains("氣泡", lyrics);
        Assert.Contains("同義反覆 握緊的手裡留下氣泡", lyrics);
        Assert.DoesNotContain("¹", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("路人粉", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("包涵", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("小小理解", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("標橘字", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("註釋", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("套套邏輯", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("我翻", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("我對這首歌", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("暫且不談", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("隱喻", lyrics, StringComparison.Ordinal);
    }

    [Fact]
    public void Fixture_html_extracts_abuku_lyrics_without_translator_commentary()
    {
        var html = BahamutFixture.ReadAbukuArtwork();
        var text = BahamutParser.ExtractArticleText(html);
        Assert.DoesNotContain("套套邏輯", text, StringComparison.Ordinal);
        Assert.DoesNotContain("上一篇", text, StringComparison.Ordinal);

        var lyrics = BahamutParser.ExtractTraditionalChineseLyrics(text);
        Assert.NotNull(lyrics);
        Assert.Contains("啊啊 無可救藥地 在我心底蠢蠢欲動的野獸", lyrics);
        Assert.Contains("同義反覆 握緊的手裡留下氣泡", lyrics);
        Assert.DoesNotContain("路人粉", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("包涵", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("小小理解", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("標橘字", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("套套邏輯", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("暫且不談", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("¹", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("あぁ", lyrics, StringComparison.Ordinal);
        Assert.True(BahamutParser.IsChromeOrNote("只是經常聽到他們的歌，充其量也只能算路人粉？"));
        Assert.True(BahamutParser.IsChromeOrNote("最後也會附上我對這首歌的小小理解～"));
    }

    [Fact]
    public async Task Live_or_fixture_sn_6324990_extracts_lyrics_without_preface()
    {
        var html = await BahamutFixture.LoadAbukuArtworkAsync();
        var lyrics = BahamutParser.ExtractTraditionalChineseLyrics(BahamutParser.ExtractArticleText(html));
        Assert.NotNull(lyrics);
        Assert.Contains("無可救藥", lyrics, StringComparison.Ordinal);
        Assert.Contains("氣泡", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("路人粉", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("包涵", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("小小理解", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("標橘字", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("套套邏輯", lyrics, StringComparison.Ordinal);
        Assert.DoesNotContain("暫且不談", lyrics, StringComparison.Ordinal);
    }

    private static TrackQuery Song(string title, string artist) =>
        TrackNormalizer.FromRaw(title, artist, null, TimeSpan.FromSeconds(260), "Chrome", PlayerKind.Browser, true);

    internal static TrackQuery AppleSunny() =>
        TrackNormalizer.FromRaw(
            "Sunny",
            "Yorushika",
            "second person",
            TimeSpan.FromSeconds(268),
            "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App",
            PlayerKind.AppleMusic,
            true);

    internal static TrackQuery YoutubeAoiShiori() =>
        TrackNormalizer.FromRaw(
            "Aoi Shiori",
            "Galileo Galilei",
            null,
            TimeSpan.FromSeconds(337),
            "Chrome",
            PlayerKind.Browser,
            true);
}
