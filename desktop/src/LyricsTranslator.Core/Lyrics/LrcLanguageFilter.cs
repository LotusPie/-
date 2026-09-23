using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Lyrics;

/// <summary>
/// Accept only real <c>[mm:ss.xx]</c> LRC in the song's language. Never invent timestamps.
/// </summary>
public static class LrcLanguageFilter
{
    public static bool HasTimestamps(string? lrc) =>
        LrcParser.Parse(lrc).Count >= 3;

    public static bool Fits(string? lrc, LyricLanguage language)
    {
        var cleaned = Clean(lrc);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return false;
        }

        var lines = LrcParser.Parse(cleaned);
        if (lines.Count < 3)
        {
            return false;
        }

        var texts = lines.Select(static l => l.Text).ToList();
        return language switch
        {
            LyricLanguage.Japanese => texts.Count(LanguageDetector.LooksLikeJapanese) >= 3,
            LyricLanguage.Korean => texts.Count(LanguageDetector.LooksLikeKorean) >= 3,
            LyricLanguage.English => texts.Count(static t =>
                                           LanguageDetector.LooksLikeJapanese(t) ||
                                           LanguageDetector.LooksLikeKorean(t)) == 0,
            _ => true,
        };
    }

    public static string? Clean(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc))
        {
            return null;
        }

        var kept = new List<string>();
        foreach (var raw in lrc.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var text = LrclibClient.StripLrcTimestamps(raw)?.Trim() ?? string.Empty;
            if (text.Length == 0 || IsCreditLine(text))
            {
                continue;
            }

            kept.Add(raw.TrimEnd());
        }

        var joined = string.Join('\n', kept).Trim();
        return HasTimestamps(joined) ? joined : null;
    }

    public static bool IsCreditLine(string text) =>
        text.StartsWith("作詞", StringComparison.Ordinal) ||
        text.StartsWith("作词", StringComparison.Ordinal) ||
        text.StartsWith("作曲", StringComparison.Ordinal) ||
        text.StartsWith("編曲", StringComparison.Ordinal) ||
        text.StartsWith("编曲", StringComparison.Ordinal) ||
        text.StartsWith("歌：", StringComparison.Ordinal) ||
        text.StartsWith("歌:", StringComparison.Ordinal);
}
