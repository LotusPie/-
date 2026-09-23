namespace LyricsTranslator.Core.Normalization;

/// <summary>
/// Small v1 alias table for JP/KR/EN artist names. Matching is on already-normalized tokens.
/// </summary>
public static class ArtistAliases
{
    private static readonly Dictionary<string, string> Canonical = new(StringComparer.Ordinal)
    {
        ["kenshi yonezu"] = "米津玄師",
        ["yonezu kenshi"] = "米津玄師",
        ["米津玄師"] = "米津玄師",
        ["bts"] = "bts",
        ["방탄소년단"] = "bts",
        ["bangtan"] = "bts",
        ["bangtan boys"] = "bts",
        ["blackpink"] = "blackpink",
        ["블랙핑크"] = "blackpink",
        ["yoasobi"] = "yoasobi",
        ["요아소비"] = "yoasobi",
        ["newjeans"] = "newjeans",
        ["뉴진스"] = "newjeans",
        ["adele"] = "adele",
        ["yorushika"] = "yorushika",
        ["ヨルシカ"] = "yorushika",
    };

    public static string Canonicalize(string normalizedArtist)
    {
        if (string.IsNullOrWhiteSpace(normalizedArtist))
        {
            return string.Empty;
        }

        return Canonical.TryGetValue(normalizedArtist, out var canon)
            ? TrackNormalizer.NormalizeToken(canon)
            : normalizedArtist;
    }

    public static IReadOnlyList<string> Variants(string artist)
    {
        var results = new List<string>();
        Add(results, artist);

        var normalized = TrackNormalizer.NormalizeToken(artist);
        if (normalized.Length == 0)
        {
            return results;
        }

        var canon = Canonicalize(normalized);
        Add(results, canon);

        foreach (var (alias, mapped) in Canonical)
        {
            var mappedNorm = TrackNormalizer.NormalizeToken(mapped);
            if (string.Equals(mappedNorm, canon, StringComparison.Ordinal) ||
                string.Equals(alias, canon, StringComparison.Ordinal) ||
                string.Equals(alias, normalized, StringComparison.Ordinal))
            {
                Add(results, alias);
                Add(results, mapped);
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
