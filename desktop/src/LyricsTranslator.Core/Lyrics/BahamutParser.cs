using System.Text.RegularExpressions;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;

namespace LyricsTranslator.Core.Lyrics;

public sealed record BahamutSearchHit(string Sn, string Title, string Url);

public sealed record CommunityTranslation(string Translation, string SourceTitle, string Url, string SiteLabel = "巴哈姆特");

public static partial class BahamutParser
{
    /// <summary>
    /// Search any foreign-language track. Skip only when the title already reads as 繁中 lyrics.
    /// Do not require Japanese kana — English/Korean/etc. still go to Bahamut.
    /// </summary>
    public static bool ShouldSearch(TrackQuery query)
    {
        if (!query.HasIdentity)
        {
            return false;
        }

        var title = query.DisplayTitle;
        if (LanguageDetector.LooksLikeJapanese(title) || LanguageDetector.LooksLikeKorean(title))
        {
            return true;
        }

        if (LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(title))
        {
            return false;
        }

        return true;
    }

    public const int MaxSearchQueries = 20;

    private static readonly string[] LyricQuerySuffixes =
    [
        "歌詞",
        "歌詞翻譯",
        "中日歌詞",
        "日+羅+中",
        "中文歌詞",
        "中文翻譯",
    ];

    public static IReadOnlyList<string> BuildSearchQueries(TrackQuery query)
    {
        var titles = TrackLookup.Titles(query);
        var artists = TrackLookup.Artists(query);
        var queries = new List<string>();
        if (titles.Count == 0)
        {
            return queries;
        }

        var title = titles[0];
        var artist = artists.Count > 0 ? artists[0] : string.Empty;

        // Native script first (花一匁, 晴る, ヨルシカ) — do not lead with Apple Music romaji.
        TryAdd(queries, title);
        foreach (var suffix in LyricQuerySuffixes)
        {
            TryAdd(queries, $"{title} {suffix}");
        }

        if (artist.Length > 0)
        {
            TryAdd(queries, $"{title} {artist}");
            TryAdd(queries, $"{title} {artist} 歌詞");
            TryAdd(queries, $"{title} {artist} 歌詞翻譯");
        }

        foreach (var aliasTitle in titles.Skip(1))
        {
            TryAdd(queries, aliasTitle);
            TryAdd(queries, $"{aliasTitle} 歌詞");
            TryAdd(queries, $"{aliasTitle} 歌詞翻譯");
            TryAdd(queries, $"{aliasTitle} 中日歌詞");
            if (artist.Length > 0)
            {
                TryAdd(queries, $"{aliasTitle} {artist}");
                TryAdd(queries, $"{aliasTitle} {artist} 歌詞");
                TryAdd(queries, $"{aliasTitle} {artist} 歌詞翻譯");
            }
        }

        foreach (var aliasArtist in artists.Skip(1))
        {
            TryAdd(queries, $"{title} {aliasArtist}");
            TryAdd(queries, $"{title} {aliasArtist} 歌詞");
            TryAdd(queries, $"{title} {aliasArtist} 歌詞翻譯");
        }

        return queries;
    }

    public static string BuildSearchUrl(TrackQuery query) =>
        BuildSearchUrl(PreferredKeyword(query));

    public static string BuildSearchUrl(string keyword) =>
        "https://home.gamer.com.tw/search.php?kw=" + Uri.EscapeDataString(keyword);

    public static string PreferredKeyword(TrackQuery query)
    {
        var title = TrackLookup.PrimaryTitle(query);
        var artist = TrackLookup.PrimaryArtist(query).Trim();
        return artist.Length > 0
            ? $"{title} {artist} 歌詞翻譯"
            : $"{title} 歌詞翻譯";
    }

    public static IReadOnlyList<BahamutSearchHit> ParseSearchHits(string html)
    {
        var hits = new List<BahamutSearchHit>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in CreationLinkRegex().Matches(html))
        {
            var sn = match.Groups["sn"].Value;
            var title = HtmlDecode(match.Groups["title"].Value).Trim();
            if (title.Length == 0 || !seen.Add(sn))
            {
                continue;
            }

            hits.Add(new BahamutSearchHit(sn, title, $"https://home.gamer.com.tw/artwork.php?sn={sn}"));
        }

        return hits;
    }

    public static int ScoreHit(BahamutSearchHit hit, TrackQuery query, string? searchKeyword = null)
    {
        var title = hit.Title;
        if (IsJunkTitle(title))
        {
            return 0;
        }

        var lyricHint = LyricHintScore(title);
        if (lyricHint <= 0)
        {
            return 0;
        }

        var compactHit = CompactForMatch(title);
        var titleVariants = TrackLookup.Titles(query)
            .Select(CompactForMatch)
            .Where(static compact => compact.Length >= 2)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var titleInHit = titleVariants.Count > 0 &&
                         titleVariants.Any(compact => compactHit.Contains(compact, StringComparison.Ordinal));
        var compactKeyword = CompactForMatch(searchKeyword ?? string.Empty);
        var keywordLinksTitle = compactKeyword.Length >= 2 &&
                                titleVariants.Any(compact => compactKeyword.Contains(compact, StringComparison.Ordinal));
        var compactTitle = CompactForMatch(query.DisplayTitle);
        var compactArtist = CompactForMatch(query.DisplayArtist);
        var artistMatched = TrackLookup.Artists(query)
            .Select(CompactForMatch)
            .Any(compact => compact.Length >= 2 && compactHit.Contains(compact, StringComparison.Ordinal));

        // Romaji SMTC (Aoi Shiori) vs Japanese post title (青い栞): Bahamut search still
        // returned this hit for our title 歌詞 query, artist matches, and it is a 中日/日+羅+中 post.
        if (!titleInHit)
        {
            if (!(artistMatched && lyricHint >= 50 && keywordLinksTitle))
            {
                return 0;
            }
        }

        var nativeTitleInHit = titleVariants.Any(compact =>
            !IsShortLatin(compact) && compactHit.Contains(compact, StringComparison.Ordinal));
        if (IsShortLatin(compactTitle) && compactArtist.Length >= 2 && !artistMatched && !nativeTitleInHit)
        {
            return 0;
        }

        var score = lyricHint + 100;
        if (artistMatched)
        {
            score += 40;
        }

        if (titleInHit)
        {
            score += 20;
        }

        if (title.Contains("填詞", StringComparison.Ordinal))
        {
            score -= 30;
        }

        return score;
    }

    public static string CompactForMatch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = TrackNormalizer.NormalizeToken(value);
        return MatchNoiseRegex().Replace(normalized, string.Empty);
    }

    public static string ExtractArticleText(string html)
    {
        var slice = SliceArticle(html);
        slice = ScriptRegex().Replace(slice, string.Empty);
        slice = StyleRegex().Replace(slice, string.Empty);
        slice = BrRegex().Replace(slice, "\n");
        slice = TagRegex().Replace(slice, string.Empty);
        return HtmlDecode(slice);
    }

    public static string? ExtractNativeTitle(string? hitTitle) =>
        TrackLookup.FirstNativePhrase(hitTitle, NativeTitleJunk);

    public static string? ExtractNativeArtist(string? hitTitle)
    {
        var title = ExtractNativeTitle(hitTitle);
        var runs = TrackLookup.NativeSegments(hitTitle);
        return runs.FirstOrDefault(run =>
            !NativeTitleJunk.Any(junk => run.Contains(junk, StringComparison.Ordinal)) &&
            !string.Equals(run, title, StringComparison.Ordinal));
    }

    public static string? ExtractTraditionalChineseLyrics(string articleText)
    {
        var lines = articleText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(static l => l.Trim())
            .Where(static l => l.Length > 0)
            .ToList();

        var chinese = new List<string>();
        foreach (var line in lines)
        {
            if (IsChromeOrNote(line))
            {
                continue;
            }

            var extracted = ExtractChineseFromLine(line);
            if (string.IsNullOrWhiteSpace(extracted) || IsChromeOrNote(extracted))
            {
                continue;
            }

            if (chinese.Count > 0 && string.Equals(chinese[^1], extracted, StringComparison.Ordinal))
            {
                continue;
            }

            chinese.Add(extracted);
        }

        if (chinese.Count < 4)
        {
            return null;
        }

        return string.Join('\n', chinese);
    }

    public static string SiteLabelFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "網頁";
        }

        if (url.Contains("gamer.com.tw", StringComparison.OrdinalIgnoreCase))
        {
            return "巴哈姆特";
        }

        if (url.Contains("mojim.com", StringComparison.OrdinalIgnoreCase))
        {
            return "Mojim";
        }

        try
        {
            return new Uri(url).Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return "網頁";
        }
    }

    public static LyricsSource SourceFromSite(string siteLabel) =>
        string.Equals(siteLabel, "巴哈姆特", StringComparison.Ordinal) ? LyricsSource.Bahamut : LyricsSource.Web;

    public static bool IsChromeOrNote(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        var text = line.Trim();
        if (text.Contains("上一篇", StringComparison.Ordinal) ||
            text.Contains("下一篇", StringComparison.Ordinal) ||
            text.Contains("留言", StringComparison.Ordinal) ||
            text.Contains("article_content", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("text-paragraph", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("article_container", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("id=\"", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("class=\"", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("b23.tv", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("中文翻譯來源", StringComparison.Ordinal) ||
            text.Contains("翻譯來源", StringComparison.Ordinal) ||
            text.Contains("不是我翻譯", StringComparison.Ordinal) ||
            text.Contains("只是把中日", StringComparison.Ordinal) ||
            text.Contains("整理起來", StringComparison.Ordinal))
        {
            return true;
        }

        return IsMetaLine(text);
    }

    private static readonly string[] NativeTitleJunk =
    [
        "中日歌詞", "中韓歌詞", "中英歌詞", "中文歌詞", "歌詞翻譯", "中文翻譯",
        "全曲翻譯", "巴哈姆特", "創作大廳", "歌詞中文翻譯", "日羅中",
    ];

    private static readonly string[] ArticleEndNeedles =
    [
        "id=\"commentRow\"",
        "dynamic-reply",
        "作者相關創作",
        "相關創作",
        "ct-btn-box",
        "tags-container",
        "sticky-bottom",
        "id=\"replys\"",
        "上一篇",
        "下一篇",
    ];

    private static string SliceArticle(string html)
    {
        var attr = html.IndexOf("id=\"article_content\"", StringComparison.OrdinalIgnoreCase);
        string slice;
        if (attr >= 0)
        {
            var gt = html.IndexOf('>', attr);
            slice = gt >= 0 ? html[(gt + 1)..] : html[attr..];
        }
        else
        {
            slice = html;
        }

        var end = IndexOfAny(slice, ArticleEndNeedles);
        return end > 0 ? slice[..end] : slice;
    }

    private static void TryAdd(List<string> queries, string value)
    {
        if (queries.Count >= MaxSearchQueries)
        {
            return;
        }

        Add(queries, value);
    }

    private static void Add(List<string> queries, string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        if (!queries.Contains(trimmed, StringComparer.Ordinal))
        {
            queries.Add(trimmed);
        }
    }

    private static bool IsJunkTitle(string title) =>
        title.Contains("遊記", StringComparison.Ordinal) ||
        title.Contains("排行", StringComparison.Ordinal) ||
        title.Contains("自我介紹", StringComparison.Ordinal) ||
        title.Contains("Billboard", StringComparison.OrdinalIgnoreCase) ||
        (title.Contains("Live", StringComparison.OrdinalIgnoreCase) && !title.Contains("歌詞", StringComparison.Ordinal));

    private static int LyricHintScore(string title)
    {
        if (title.Contains("歌詞翻譯", StringComparison.Ordinal) ||
            title.Contains("中日歌詞", StringComparison.Ordinal) ||
            title.Contains("日+羅+中", StringComparison.Ordinal) ||
            title.Contains("日羅中", StringComparison.Ordinal) ||
            title.Contains("中韓歌詞", StringComparison.Ordinal) ||
            title.Contains("中英歌詞", StringComparison.Ordinal))
        {
            return 60;
        }

        if (title.Contains("中文歌詞", StringComparison.Ordinal) ||
            title.Contains("中日對照", StringComparison.Ordinal) ||
            title.Contains("中韓對照", StringComparison.Ordinal) ||
            title.Contains("中英對照", StringComparison.Ordinal))
        {
            return 50;
        }

        if ((title.Contains("歌詞", StringComparison.Ordinal) && title.Contains("翻譯", StringComparison.Ordinal)) ||
            title.Contains("全曲翻譯", StringComparison.Ordinal) ||
            title.Contains("譯詞", StringComparison.Ordinal))
        {
            return 50;
        }

        if (title.Contains("歌詞", StringComparison.Ordinal) ||
            title.Contains("翻譯", StringComparison.Ordinal) ||
            title.Contains("對照", StringComparison.Ordinal))
        {
            return 15;
        }

        return 0;
    }

    private static bool IsShortLatin(string compactTitle) =>
        compactTitle.Length > 0 && compactTitle.Length <= 8 && compactTitle.All(static c => c < 128);

    private static string? ExtractChineseFromLine(string line)
    {
        var marker = line.IndexOf('▍');
        if (marker >= 0)
        {
            line = line[(marker + 1)..];
        }

        var tagged = ChineseTagRegex().Match(line);
        if (tagged.Success)
        {
            line = tagged.Groups[1].Value;
        }

        var parts = InterleavedSplitRegex()
            .Split(line)
            .Select(static part => ExtractChineseSegment(part))
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        return parts.Count == 0 ? null : string.Join(" ", parts!);
    }

    private static string? ExtractChineseSegment(string line)
    {
        var paren = line.IndexOf('（');
        if (paren > 0)
        {
            line = line[..paren];
        }

        line = line.Trim();
        if (line.Length == 0 || LanguageDetector.LooksLikeJapanese(line) || LanguageDetector.LooksLikeKorean(line))
        {
            return null;
        }

        if (!LanguageDetector.HasHan(line))
        {
            return null;
        }

        return line;
    }

    private static bool IsMetaLine(string line) =>
        line.StartsWith("作詞", StringComparison.Ordinal) ||
        line.StartsWith("作曲", StringComparison.Ordinal) ||
        line.StartsWith("編曲", StringComparison.Ordinal) ||
        line.StartsWith("中文翻譯", StringComparison.Ordinal) ||
        line.StartsWith("歌詞翻譯", StringComparison.Ordinal) ||
        line.StartsWith("翻譯：", StringComparison.Ordinal) ||
        line.StartsWith("歌：", StringComparison.Ordinal) ||
        line.Contains("翻譯錯誤", StringComparison.Ordinal) ||
        line.Contains("請見諒", StringComparison.Ordinal) ||
        line.Contains("轉載", StringComparison.Ordinal) ||
        line.Contains("繼續閱讀", StringComparison.Ordinal) ||
        line.Contains("作者相關", StringComparison.Ordinal) ||
        line.Contains("來源：", StringComparison.Ordinal) ||
        line.Contains("來源:", StringComparison.Ordinal);

    private static int IndexOfAny(string text, params string[] needles)
    {
        var best = -1;
        foreach (var needle in needles)
        {
            var found = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (found >= 0 && (best < 0 || found < best))
            {
                best = found;
            }
        }

        return best;
    }

    private static int IndexOfAny(string text, IReadOnlyList<string> needles)
    {
        var best = -1;
        foreach (var needle in needles)
        {
            var found = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (found >= 0 && (best < 0 || found < best))
            {
                best = found;
            }
        }

        return best;
    }

    private static string HtmlDecode(string value)
    {
        return value
            .Replace("&amp;", "&", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal)
            .Replace("&#039;", "'", StringComparison.Ordinal)
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&nbsp;", " ", StringComparison.Ordinal)
            .Replace("&#x27;", "'", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"[\s「」『』""'（）()\[\]【】\-–—−・·,，.。!！?？:：]+", RegexOptions.CultureInvariant)]
    private static partial Regex MatchNoiseRegex();

    [GeneratedRegex(
        @"(?:creationDetail|artwork)\.php\?sn=(?<sn>\d+)[^>]*>\s*(?<title>[^<]{1,240})",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CreationLinkRegex();

    [GeneratedRegex(@"^【(?:中|繁中|譯|中文|翻譯)】\s*(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseTagRegex();

    [GeneratedRegex(@"<script[\s\S]*?</script>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptRegex();

    [GeneratedRegex(@"<style[\s\S]*?</style>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StyleRegex();

    [GeneratedRegex(@"<br\s*/?>|</p>|</div>|</li>|<li[^>]*>|</h[1-6]>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BrRegex();

    // Same-line JP/ZH pairs: "貴方は風のように / 你就像微風一般" or fullwidth ／.
    // Require spaces around ASCII / so titles like 中日歌詞/中文翻譯 stay intact.
    [GeneratedRegex(@"\s*／\s*|\s+/\s+", RegexOptions.CultureInvariant)]
    private static partial Regex InterleavedSplitRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();
}
