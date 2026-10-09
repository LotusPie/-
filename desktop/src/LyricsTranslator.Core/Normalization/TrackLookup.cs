using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Normalization;

/// <summary>
/// Prefer the script on the now-playing line: Japanese kana/kanji first, not Apple Music romaji.
/// </summary>
public static class TrackLookup
{
    public static IReadOnlyList<string> Titles(TrackQuery query)
    {
        var values = new List<string>();
        AddRange(values, TitleAliases.Variants(query.DisplayTitle));
        AddRange(values, NativeSegments(query.DisplayTitle));
        Add(values, query.RecoveredTitle);
        if (values.Count == 0)
        {
            Add(values, query.DisplayTitle);
        }

        return NativeFirst(values, query.DisplayTitle);
    }

    public static IReadOnlyList<string> Artists(TrackQuery query)
    {
        var values = new List<string>();
        AddRange(values, ArtistAliases.Variants(query.DisplayArtist));
        AddRange(values, NativeSegments(query.DisplayArtist));
        Add(values, query.RecoveredArtist);
        if (values.Count == 0)
        {
            Add(values, query.DisplayArtist);
        }

        return NativeFirst(values, query.DisplayArtist);
    }

    public static string PrimaryTitle(TrackQuery query) =>
        Titles(query).FirstOrDefault() ?? query.DisplayTitle;

    public static string PrimaryArtist(TrackQuery query) =>
        Artists(query).FirstOrDefault() ?? query.DisplayArtist;

    public static IReadOnlyList<string> NativeFirst(IEnumerable<string> variants, string? display = null)
    {
        var unique = new List<string>();
        foreach (var variant in variants)
        {
            Add(unique, variant);
        }

        return unique
            .OrderBy(value => ScriptRank(value, display))
            .ThenBy(value => unique.IndexOf(value))
            .ToList();
    }

    public static IReadOnlyList<string> NativeSegments(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var runs = new List<string>();
        var start = -1;
        for (var i = 0; i <= value.Length; i++)
        {
            var native = i < value.Length && IsNativeChar(value[i]);
            if (native && start < 0)
            {
                start = i;
            }
            else if (!native && start >= 0)
            {
                var run = value[start..i].Trim();
                if (run.Length >= 2)
                {
                    runs.Add(run);
                }

                start = -1;
            }
        }

        return runs;
    }

    public static string? FirstNativePhrase(string? value, IEnumerable<string>? junk = null)
    {
        var banned = junk?.ToHashSet(StringComparer.Ordinal) ?? [];
        foreach (var run in NativeSegments(value))
        {
            if (banned.Any(item => run.Contains(item, StringComparison.Ordinal)))
            {
                continue;
            }

            if (banned.Contains(run))
            {
                continue;
            }

            return run;
        }

        return null;
    }

    private static int ScriptRank(string value, string? display)
    {
        if (LanguageDetector.LooksLikeJapaneseOrKanjiTitle(value))
        {
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(display) &&
            string.Equals(value.Trim(), display.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (LanguageDetector.LooksLikeKorean(value) || LanguageDetector.HasHan(value))
        {
            return 2;
        }

        return 3;
    }

    private static bool IsNativeChar(char c) =>
        LanguageDetector.HasHan(c.ToString()) ||
        LanguageDetector.LooksLikeJapanese(c.ToString()) ||
        LanguageDetector.LooksLikeKorean(c.ToString());

    private static void AddRange(List<string> values, IEnumerable<string> incoming)
    {
        foreach (var item in incoming)
        {
            Add(values, item);
        }
    }

    private static void Add(List<string> values, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (values.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        values.Add(trimmed);
    }
}
