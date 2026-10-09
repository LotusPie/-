using LyricsTranslator.Core.Lyrics;

namespace LyricsTranslator.Core.Sync;

public sealed record OverlayWindowLine(int Index, TimedLyric Line, int Distance);

/// <summary>
/// Playhead + timed lines → current index and overlay window.
/// Like braccato's renderer: this type owns no clock; a tick arrives from outside.
/// </summary>
public sealed record SyncedLineSnapshot(
    TimeSpan Playhead,
    int CurrentIndex,
    IReadOnlyList<TimedLyric> Lines,
    IReadOnlyList<OverlayWindowLine> Window,
    bool IndexChanged,
    bool HasLines,
    bool HasTiming);

public sealed class SyncedLineStream
{
    public const int DefaultWindowRadius = 2;

    private IReadOnlyList<TimedLyric> _lines = [];
    private int _index = -1;

    public IReadOnlyList<TimedLyric> Lines => _lines;

    public int CurrentIndex => _index;

    public void Reset(IReadOnlyList<TimedLyric> lines)
    {
        _lines = lines ?? [];
        _index = -1;
    }

    public SyncedLineSnapshot Tick(TimeSpan playhead, int windowRadius = DefaultWindowRadius)
    {
        if (_lines.Count == 0)
        {
            var emptyChanged = _index != 0;
            _index = 0;
            return new SyncedLineSnapshot(playhead, 0, _lines, [], emptyChanged, false, false);
        }

        var index = LyricTrack.IndexAt(_lines, playhead);
        var changed = index != _index;
        _index = index;
        return new SyncedLineSnapshot(
            playhead,
            index,
            _lines,
            BuildWindow(_lines, index, windowRadius),
            changed,
            true,
            HasAnyTimestamp(_lines));
    }

    public static IReadOnlyList<OverlayWindowLine> BuildWindow(
        IReadOnlyList<TimedLyric> lines,
        int index,
        int windowRadius = DefaultWindowRadius)
    {
        if (lines.Count == 0)
        {
            return [];
        }

        var start = Math.Max(0, index - windowRadius);
        var end = Math.Min(lines.Count - 1, index + windowRadius);
        var window = new OverlayWindowLine[end - start + 1];
        for (var i = start; i <= end; i++)
        {
            window[i - start] = new OverlayWindowLine(i, lines[i], Math.Abs(i - index));
        }

        return window;
    }

    private static bool HasAnyTimestamp(IReadOnlyList<TimedLyric> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Timestamp is not null)
            {
                return true;
            }
        }

        return false;
    }
}
