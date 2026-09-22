using System.Globalization;
using System.Text.RegularExpressions;

namespace LyricsTranslator.Core.Lyrics;

public static partial class LrcParser
{
    public sealed record Line(TimeSpan Timestamp, string Text);

    public static IReadOnlyList<Line> Parse(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc))
        {
            return [];
        }

        var lines = new List<Line>();
        foreach (var raw in lrc.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var matches = TimestampRegex().Matches(raw);
            if (matches.Count == 0)
            {
                continue;
            }

            var text = TimestampRegex().Replace(raw, string.Empty).Trim();
            if (text.Length == 0)
            {
                continue;
            }

            foreach (Match match in matches)
            {
                if (TryParseTimestamp(match, out var ts))
                {
                    lines.Add(new Line(ts, text));
                }
            }
        }

        return lines
            .OrderBy(l => l.Timestamp)
            .GroupBy(l => l.Timestamp)
            .Select(g => g.First())
            .ToList();
    }

    private static bool TryParseTimestamp(Match match, out TimeSpan timestamp)
    {
        timestamp = default;
        var m = match.Groups["m"].Success ? int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
        var s = int.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture);
        var frac = match.Groups["f"].Success ? match.Groups["f"].Value : "0";
        if (frac.Length == 1)
        {
            frac += "00";
        }
        else if (frac.Length == 2)
        {
            frac += "0";
        }

        if (!int.TryParse(frac.PadRight(3, '0')[..3], out var ms))
        {
            return false;
        }

        timestamp = new TimeSpan(0, 0, m, s, ms);
        return true;
    }

    [GeneratedRegex(@"\[(?:(?<m>\d{1,2}):)?(?<s>\d{1,2})\.(?<f>\d{1,3})\]|\[(?<m>\d{1,2}):(?<s>\d{2})\]", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampRegex();
}
