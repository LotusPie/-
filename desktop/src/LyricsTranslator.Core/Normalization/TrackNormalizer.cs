using System.Text;
using System.Text.RegularExpressions;
using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Normalization;

public static partial class TrackNormalizer
{
    public static TrackQuery FromRaw(
        string? title,
        string? artist,
        string? album,
        TimeSpan? duration,
        string? sourceAppId,
        PlayerKind playerKind,
        bool isPlaying)
    {
        title ??= string.Empty;
        artist ??= string.Empty;

        if (playerKind == PlayerKind.AppleMusic)
        {
            SplitAppleMusicArtistAlbum(ref artist, ref album);
        }

        if (playerKind == PlayerKind.Browser)
        {
            ParseYouTubeMusicTitle(ref title, ref artist);
        }

        var displayTitle = CollapseWhitespace(title);
        var displayArtist = CollapseWhitespace(artist);
        var displayAlbum = string.IsNullOrWhiteSpace(album) ? null : CollapseWhitespace(album);

        var normalizedTitle = NormalizeToken(StripTitleNoise(displayTitle));
        var normalizedArtist = NormalizeToken(StripArtistNoise(displayArtist));
        normalizedArtist = ArtistAliases.Canonicalize(normalizedArtist);

        var cacheKey = BuildCacheKey(normalizedArtist, normalizedTitle);

        return new TrackQuery(
            DisplayTitle: string.IsNullOrWhiteSpace(displayTitle) ? "未知歌曲" : displayTitle,
            DisplayArtist: displayArtist,
            Album: displayAlbum,
            Duration: duration,
            NormalizedTitle: normalizedTitle,
            NormalizedArtist: normalizedArtist,
            CacheKey: cacheKey,
            SourceAppId: sourceAppId,
            PlayerKind: playerKind,
            IsPlaying: isPlaying);
    }

    public static string BuildCacheKey(string normalizedArtist, string normalizedTitle)
        => $"{normalizedArtist}\t{normalizedTitle}";

    public static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var formKc = value.Normalize(NormalizationForm.FormKC);
        formKc = formKc.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"');
        formKc = CollapseWhitespace(formKc);
        return formKc.ToLowerInvariant();
    }

    public static string StripTitleNoise(string title)
    {
        var text = title;
        text = TitleNoiseRegex().Replace(text, " ");
        text = FeatRegex().Replace(text, " ");
        return CollapseWhitespace(text);
    }

    public static string StripArtistNoise(string artist)
    {
        var text = FeatRegex().Replace(artist, " ");
        text = TopicChannelRegex().Replace(text, " ");
        return CollapseWhitespace(text);
    }

    public static void SplitAppleMusicArtistAlbum(ref string artist, ref string? album)
    {
        if (!string.IsNullOrWhiteSpace(album))
        {
            return;
        }

        var parts = SplitOnDash(artist, 2);
        if (parts.Length != 2)
        {
            return;
        }

        artist = parts[0];
        album = parts[1];
    }

    public static void ParseYouTubeMusicTitle(ref string title, ref string artist)
    {
        title = TitleNoiseRegex().Replace(title, " ");
        title = CollapseWhitespace(title);

        var artistLooksLikeChannel = LooksLikeChannelName(artist);
        var parts = SplitOnDash(title, 2);
        if (parts.Length != 2)
        {
            return;
        }

        var left = CollapseWhitespace(parts[0]);
        var right = CollapseWhitespace(parts[1]);
        if (left.Length == 0 || right.Length == 0)
        {
            return;
        }

        if (artistLooksLikeChannel || string.IsNullOrWhiteSpace(artist) ||
            string.Equals(NormalizeToken(artist), NormalizeToken(left), StringComparison.Ordinal) ||
            NormalizeToken(artist).Contains("vevo", StringComparison.Ordinal))
        {
            artist = left;
            title = right;
        }
        else if (string.Equals(NormalizeToken(artist), NormalizeToken(right), StringComparison.Ordinal))
        {
            title = left;
        }
    }

    public static bool LooksLikeChannelName(string? artist)
    {
        if (string.IsNullOrWhiteSpace(artist))
        {
            return true;
        }

        var n = NormalizeToken(artist);
        return n.Contains("vevo", StringComparison.Ordinal)
               || n.EndsWith("topic", StringComparison.Ordinal)
               || n.Contains("records", StringComparison.Ordinal)
               || n.Contains("official", StringComparison.Ordinal)
               || n is "youtube" or "youtube music";
    }

    public static string[] SplitOnDash(string value, int count)
    {
        foreach (var dash in new[] { " — ", " – ", " − ", " - " })
        {
            if (value.Contains(dash, StringComparison.Ordinal))
            {
                return value.Split(dash, count, StringSplitOptions.TrimEntries);
            }
        }

        return [value];
    }

    public static string CollapseWhitespace(string value)
        => WhitespaceRegex().Replace(value.Trim(), " ");

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(
        """
        (?:
            \s*[\(\[\{【]
            \s*(?:official\s*(?:music\s*)?(?:lyric\s*)?video|official\s*audio|lyric\s*video|lyrics?|audio|visualizer|mv|m\s*/\s*v|music\s*video|hd|4k|remaster(?:ed)?|color\s*coded)
            \s*[\)\]\}】]
          | \s*-\s*topic\b
          | \s*\|\s*topic\b
          | \s*\(official\)
        )
        """,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex TitleNoiseRegex();

    [GeneratedRegex(
        @"\s*[\(\[\{【]\s*(?:feat\.?|ft\.?|featuring)\s+[^\)\]\}】]+[\)\]\}】]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FeatRegex();

    [GeneratedRegex(@"\s*-\s*topic\b|\s*vevo\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TopicChannelRegex();
}
