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

    public static IReadOnlyList<string> BuildSearchQueries(TrackQuery query)
    {
        var title = query.DisplayTitle.Trim();
        var artist = query.DisplayArtist.Trim();
        var queries = new List<string>();
        Add(queries, title);
        if (artist.Length > 0)
        {
            Add(queries, $"{title} {artist}");
        }

        Add(queries, $"{title} 歌詞");
        Add(queries, $"{title} 歌詞翻譯");
        Add(queries, $"{title} 中文歌詞");
        if (artist.Length > 0)
        {
            Add(queries, $"{title} {artist} 歌詞");
            Add(queries, $"{title} {artist} 歌詞翻譯");
        }

        return queries;
    }

    public static string BuildSearchUrl(TrackQuery query) =>
        BuildSearchUrl(PreferredKeyword(query));

    public static string BuildSearchUrl(string keyword) =>
        "https://home.gamer.com.tw/search.php?kw=" + Uri.EscapeDataString(keyword);

    public static string PreferredKeyword(TrackQuery query)
    {
        var artist = query.DisplayArtist.Trim();
        return artist.Length > 0
            ? $"{query.DisplayTitle.Trim()} {artist} 歌詞翻譯"
            : $"{query.DisplayTitle.Trim()} 歌詞翻譯";
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

    public static int ScoreHit(BahamutSearchHit hit, TrackQuery query)
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
        var compactTitle = CompactForMatch(query.DisplayTitle);
        if (compactTitle.Length < 2 || !compactHit.Contains(compactTitle, StringComparison.Ordinal))
        {
            return 0;
        }

        var compactArtist = CompactForMatch(query.DisplayArtist);
        var artistMatched = compactArtist.Length >= 2 &&
                            compactHit.Contains(compactArtist, StringComparison.Ordinal);

        // Short Latin titles ("Hello", "Stay") match too many unrelated posts unless the artist is there.
        if (IsShortLatin(compactTitle) && compactArtist.Length >= 2 && !artistMatched)
        {
            return 0;
        }

        var score = lyricHint + 100;
        if (artistMatched)
        {
            score += 40;
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
        var start = html.IndexOf("id=\"article_content\"", StringComparison.OrdinalIgnoreCase);
        var slice = start >= 0 ? html[start..] : html;
        var end = IndexOfAny(slice, "id=\"commentRow\"", "dynamic-reply", "作者相關創作", "相關創作");
        if (end > 0)
        {
            slice = slice[..end];
        }

        slice = ScriptRegex().Replace(slice, string.Empty);
        slice = BrRegex().Replace(slice, "\n");
        slice = TagRegex().Replace(slice, string.Empty);
        return HtmlDecode(slice);
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
            var extracted = ExtractChineseFromLine(line);
            if (string.IsNullOrWhiteSpace(extracted) || IsMetaLine(extracted))
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
        line.StartsWith("翻譯：", StringComparison.Ordinal) ||
        line.StartsWith("歌：", StringComparison.Ordinal) ||
        line.Contains("轉載", StringComparison.Ordinal) ||
        line.Contains("繼續閱讀", StringComparison.Ordinal) ||
        line.Contains("作者相關", StringComparison.Ordinal);

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

    [GeneratedRegex(@"<br\s*/?>|</p>|</div>|</li>|<li[^>]*>|</h[1-6]>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BrRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();
}
