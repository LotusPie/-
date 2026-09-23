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
            : Split(original).Where(static l => !BahamutParser.IsChromeOrNote(l)).ToList();
        var translationLines = Split(translation).Where(static l => !BahamutParser.IsChromeOrNote(l)).ToList();
        var displayCount = translationLines.Count > 0
            ? translationLines.Count
            : timedOriginal.Count > 0
                ? timedOriginal.Count
                : originalLines.Count;
        if (displayCount == 0)
        {
            return [];
        }

        var result = new TimedLyric[displayCount];
        for (var j = 0; j < displayCount; j++)
        {
            var tran = j < translationLines.Count ? translationLines[j] : string.Empty;
            var orig = MapOriginal(originalLines, timedOriginal, j, displayCount);
            var ts = MapTimestamp(timedOriginal, j, displayCount);
            result[j] = new TimedLyric(ts, orig, tran);
        }

        return result;
    }

    /// <summary>
    /// Last displayed line whose LRC-mapped timestamp is ≤ position.
    /// Does not invent equal-duration slices when there is no time axis.
    /// </summary>
    public static int IndexAt(IReadOnlyList<TimedLyric> lines, TimeSpan position)
    {
        if (lines.Count == 0)
        {
            return 0;
        }

        var idx = 0;
        var found = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Timestamp is { } ts && ts <= position)
            {
                idx = i;
                found = true;
            }
        }

        return found ? idx : 0;
    }

    private static TimeSpan? MapTimestamp(IReadOnlyList<LrcParser.Line> lrc, int displayIndex, int displayCount)
    {
        if (lrc.Count == 0)
        {
            return null;
        }

        if (displayCount <= 1)
        {
            return lrc[0].Timestamp;
        }

        if (lrc.Count == 1)
        {
            return displayIndex == 0 ? lrc[0].Timestamp : null;
        }

        var src = displayIndex * (lrc.Count - 1) / (double)(displayCount - 1);
        var lo = (int)Math.Floor(src);
        lo = Math.Clamp(lo, 0, lrc.Count - 1);
        var hi = Math.Min(lo + 1, lrc.Count - 1);
        var frac = src - lo;
        if (hi == lo || frac <= 0)
        {
            return lrc[lo].Timestamp;
        }

        var a = lrc[lo].Timestamp.Ticks;
        var b = lrc[hi].Timestamp.Ticks;
        return TimeSpan.FromTicks(a + (long)((b - a) * frac));
    }

    private static string MapOriginal(
        IReadOnlyList<string> originalLines,
        IReadOnlyList<LrcParser.Line> lrc,
        int displayIndex,
        int displayCount)
    {
        if (originalLines.Count == displayCount)
        {
            return originalLines[displayIndex];
        }

        if (lrc.Count == 0)
        {
            return displayIndex < originalLines.Count ? originalLines[displayIndex] : string.Empty;
        }

        if (lrc.Count == 1 || displayCount <= 1)
        {
            return lrc[0].Text;
        }

        var src = displayIndex * (lrc.Count - 1) / (double)(displayCount - 1);
        var nearest = (int)Math.Round(src, MidpointRounding.AwayFromZero);
        nearest = Math.Clamp(nearest, 0, lrc.Count - 1);
        return lrc[nearest].Text;
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
