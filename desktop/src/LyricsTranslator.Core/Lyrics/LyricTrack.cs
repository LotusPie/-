using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Lyrics;

public sealed record TimedLyric(TimeSpan? Timestamp, string Original, string Translation);

public static class LyricTrack
{
    public static IReadOnlyList<TimedLyric> Build(string? original, string? translation, string? syncedLrc)
    {
        var timedOriginal = LrcParser.Parse(syncedLrc);
        var originalLines = timedOriginal.Count > 0
            ? timedOriginal.Select(l => l.Text).ToList()
            : Split(original);
        var translationLines = Split(translation);
        var count = Math.Max(originalLines.Count, translationLines.Count);
        if (count == 0)
        {
            return [];
        }

        var result = new TimedLyric[count];
        for (var i = 0; i < count; i++)
        {
            TimeSpan? ts = i < timedOriginal.Count ? timedOriginal[i].Timestamp : null;
            var orig = i < originalLines.Count ? originalLines[i] : string.Empty;
            var tran = i < translationLines.Count ? translationLines[i] : string.Empty;
            result[i] = new TimedLyric(ts, orig, tran);
        }

        return result;
    }

    public static int IndexAt(
        IReadOnlyList<TimedLyric> lines,
        TimeSpan position,
        TimeSpan? duration)
    {
        if (lines.Count == 0)
        {
            return 0;
        }

        if (lines.Any(l => l.Timestamp is not null))
        {
            var idx = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Timestamp is { } ts && ts <= position)
                {
                    idx = i;
                }
            }

            return idx;
        }

        if (duration is { TotalMilliseconds: > 0 })
        {
            var ratio = Math.Clamp(position.TotalMilliseconds / duration.Value.TotalMilliseconds, 0, 0.999);
            return Math.Min(lines.Count - 1, (int)(ratio * lines.Count));
        }

        return 0;
    }

    private static List<string> Split(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(static l => l.TrimEnd())
            .ToList();
    }
}
