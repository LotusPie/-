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
}
