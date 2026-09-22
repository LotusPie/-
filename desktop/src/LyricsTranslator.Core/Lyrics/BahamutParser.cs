using System.Text.RegularExpressions;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;

namespace LyricsTranslator.Core.Lyrics;

public sealed record BahamutSearchHit(string Sn, string Title, string Url);

public sealed record CommunityTranslation(string Translation, string SourceTitle, string Url);

public static partial class BahamutParser
{
    public static bool ShouldSearch(TrackQuery query)
    {
        var blob = $"{query.DisplayTitle} {query.DisplayArtist}";
        if (LanguageDetector.LooksLikeKorean(blob))
        {
            return false;
        }

        return LanguageDetector.LooksLikeJapanese(blob);
    }

    public static IReadOnlyList<BahamutSearchHit> ParseSearchHits(string html)
    {
        var hits = new List<BahamutSearchHit>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in CreationLinkRegex().Matches(html))
        {
            var sn = match.Groups[1].Value;
            var title = HtmlDecode(match.Groups[2].Value).Trim();
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
        var compactHit = CompactForMatch(title);
        var score = 0;

        if (title.Contains("遊記", StringComparison.Ordinal) ||
            title.Contains("排行", StringComparison.Ordinal) ||
            title.Contains("Billboard", StringComparison.OrdinalIgnoreCase) ||
            (title.Contains("Live", StringComparison.OrdinalIgnoreCase) && !title.Contains("歌詞", StringComparison.Ordinal)))
        {
            return 0;
        }

        if (title.Contains("填詞", StringComparison.Ordinal))
        {
            score -= 30;
        }

        if (title.Contains("歌詞翻譯", StringComparison.Ordinal) || title.Contains("中日歌詞", StringComparison.Ordinal))
        {
            score += 60;
        }
        else if (title.Contains("歌詞", StringComparison.Ordinal) && title.Contains("翻譯", StringComparison.Ordinal))
        {
            score += 50;
        }
        else if (title.Contains("歌詞", StringComparison.Ordinal) || title.Contains("翻譯", StringComparison.Ordinal))
        {
            score += 15;
        }
        else
        {
            return 0;
        }

        var compactTitle = CompactForMatch(query.DisplayTitle);
        if (compactTitle.Length >= 2 && compactHit.Contains(compactTitle, StringComparison.Ordinal))
        {
            score += 100;
        }
        else
        {
            // Do not accept single-kanji overlap (愛/時/心) as a song match.
            return 0;
        }

        var compactArtist = CompactForMatch(query.DisplayArtist);
        if (compactArtist.Length >= 2 && compactHit.Contains(compactArtist, StringComparison.Ordinal))
        {
            score += 40;
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
        var end = slice.IndexOf("id=\"commentRow\"", StringComparison.OrdinalIgnoreCase);
        if (end < 0)
        {
            end = slice.IndexOf("dynamic-reply", StringComparison.OrdinalIgnoreCase);
        }

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
            if (string.IsNullOrWhiteSpace(extracted))
            {
                continue;
            }

            if (IsMetaLine(extracted))
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

    public static string BuildSearchUrl(TrackQuery query)
    {
        var q = $"{query.DisplayTitle} {query.DisplayArtist} 歌詞翻譯".Trim();
        return "https://home.gamer.com.tw/search.php?kw=" + Uri.EscapeDataString(q);
    }

    private static string? ExtractChineseFromLine(string line)
    {
        var marker = line.IndexOf('▍');
        if (marker >= 0)
        {
            line = line[(marker + 1)..];
        }

        var paren = line.IndexOf('（');
        if (paren > 0)
        {
            line = line[..paren];
        }

        line = line.Trim();
        if (line.Length == 0 || LanguageDetector.LooksLikeJapanese(line))
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
        line.Contains("繼續閱讀", StringComparison.Ordinal);

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

    [GeneratedRegex(@"creationDetail\.php\?sn=(\d+)"">([^<]{1,240})", RegexOptions.CultureInvariant)]
    private static partial Regex CreationLinkRegex();

    [GeneratedRegex(@"<script[\s\S]*?</script>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptRegex();

    [GeneratedRegex(@"<br\s*/?>|</p>|</div>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BrRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();
}
