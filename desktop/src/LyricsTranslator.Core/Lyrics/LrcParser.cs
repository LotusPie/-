using System.Globalization;

namespace LyricsTranslator.Core.Lyrics;

public static class LrcParser
{
    public sealed record Line(TimeSpan Timestamp, string Text);

    public static IReadOnlyList<Line> Parse(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc))
        {
            return [];
        }

        var lines = new List<Line>();
        var seen = new HashSet<(long Ticks, string Text)>();
        foreach (var raw in lrc.Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Replace('\r', '\n')
                     .Split('\n'))
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var stamps = new List<TimeSpan>();
            var cursor = 0;
            while (cursor < trimmed.Length && trimmed[cursor] == '[')
            {
                var close = trimmed.IndexOf(']', cursor + 1);
                if (close < 0)
                {
                    break;
                }

                var inner = trimmed[(cursor + 1)..close];
                if (!TryParseCue(inner, out var ts))
                {
                    break;
                }

                stamps.Add(ts);
                cursor = close + 1;
            }

            var text = trimmed[cursor..].Trim();
            if (stamps.Count == 0 || text.Length == 0 || LrcLanguageFilter.IsCreditLine(text))
            {
                continue;
            }

            foreach (var ts in stamps)
            {
                if (seen.Add((ts.Ticks, text)))
                {
                    lines.Add(new Line(ts, text));
                }
            }
        }

        lines.Sort(static (a, b) =>
        {
            var byTime = a.Timestamp.CompareTo(b.Timestamp);
            return byTime != 0 ? byTime : string.CompareOrdinal(a.Text, b.Text);
        });
        return lines;
    }

    /// <summary>
    /// NetEase/LRCLIB cues: <c>[mm:ss]</c>, <c>[mm:ss.xx]</c>, <c>[mm:ss.xxx]</c>,
    /// <c>[mm:ss:ff]</c>. Metadata like <c>[ti:]</c>/<c>[offset:]</c> is not a cue.
    /// </summary>
    public static bool TryParseCue(string? inner, out TimeSpan timestamp)
    {
        timestamp = default;
        if (string.IsNullOrWhiteSpace(inner))
        {
            return false;
        }

        inner = inner.Trim();
        if (!char.IsDigit(inner[0]))
        {
            return false;
        }

        string? fraction = null;
        var main = inner;
        var dot = inner.LastIndexOf('.');
        if (dot >= 0)
        {
            fraction = inner[(dot + 1)..];
            main = inner[..dot];
            if (fraction.Length is 0 or > 3 || !IsAllDigits(fraction))
            {
                return false;
            }
        }

        var parts = main.Split(':');
        if (parts.Length is < 1 or > 3)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (part.Length is 0 or > 2 || !IsAllDigits(part))
            {
                return false;
            }
        }

        int minutes;
        int seconds;
        if (parts.Length == 1)
        {
            minutes = 0;
            seconds = ParseInt(parts[0]);
        }
        else if (parts.Length == 2)
        {
            minutes = ParseInt(parts[0]);
            seconds = ParseInt(parts[1]);
        }
        else if (fraction is not null)
        {
            minutes = (ParseInt(parts[0]) * 60) + ParseInt(parts[1]);
            seconds = ParseInt(parts[2]);
        }
        else
        {
            // NetEase-style [mm:ss:ff] (colon hundredths), not hh:mm:ss.
            minutes = ParseInt(parts[0]);
            seconds = ParseInt(parts[1]);
            fraction = parts[2];
        }

        if (minutes < 0 || seconds < 0 || seconds > 59)
        {
            return false;
        }

        var ms = 0;
        if (fraction is not null)
        {
            if (fraction.Length is 0 or > 3 || !IsAllDigits(fraction))
            {
                return false;
            }

            if (!int.TryParse(fraction.PadRight(3, '0')[..3], NumberStyles.None, CultureInfo.InvariantCulture, out ms))
            {
                return false;
            }
        }

        timestamp = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(ms);
        return true;
    }

    private static int ParseInt(string value) =>
        int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);

    private static bool IsAllDigits(string value)
    {
        foreach (var c in value)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return value.Length > 0;
    }
}
