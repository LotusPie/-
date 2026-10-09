namespace LyricsTranslator.Core.Normalization;

/// <summary>
/// English Apple Music titles of Japanese songs (Sunny ↔ 晴る) so 中日歌詞 posts still match.
/// Matching is on already-normalized / compact tokens; cache keys stay on the SMTC title.
/// </summary>
public static class TitleAliases
{
    private static readonly string[][] Groups =
    [
        ["Sunny", "晴る"],
        ["花一匁", "Hanaichi Monnme", "Hanaichi Monme", "Hanaichimonme"],
        ["青い栞", "Aoi Shiori"],
    ];

    public static IReadOnlyList<string> Variants(string title)
    {
        var results = new List<string>();
        Add(results, title);

        var stripped = TrackNormalizer.StripTitleNoise(title);
        var normalized = TrackNormalizer.NormalizeToken(stripped);
        if (normalized.Length == 0)
        {
            return results;
        }

        foreach (var group in Groups)
        {
            if (!group.Any(alias => string.Equals(TrackNormalizer.NormalizeToken(alias), normalized, StringComparison.Ordinal)))
            {
                continue;
            }

            foreach (var alias in group)
            {
                Add(results, alias);
            }
        }

        return results;
    }

    private static void Add(List<string> results, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (results.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        results.Add(trimmed);
    }
}
