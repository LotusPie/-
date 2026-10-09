using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Overlay;
using LyricsTranslator.Core.Sync;

namespace LyricsTranslator.Core.Tests;

public class SyncedLineStreamTests
{
    [Fact]
    public void Tick_moves_index_with_playhead_and_builds_overlay_window()
    {
        var stream = OverlayPolicy.CreateStream(
            LyricsTranslator.Core.Models.LyricsStatus.Ready,
            "a\nb\nc\nd\ne",
            "甲\n乙\n丙\n丁\n戊",
            "[00:00.00] a\n[00:10.00] b\n[00:20.00] c\n[00:30.00] d\n[00:40.00] e");

        var first = stream.Tick(TimeSpan.FromSeconds(0));
        Assert.Equal(0, first.CurrentIndex);
        Assert.True(first.IndexChanged);
        Assert.True(first.HasTiming);
        Assert.Equal(3, first.Window.Count);
        Assert.Equal(0, first.Window[0].Index);

        var mid = stream.Tick(TimeSpan.FromSeconds(21));
        Assert.Equal(2, mid.CurrentIndex);
        Assert.True(mid.IndexChanged);
        Assert.Equal(5, mid.Window.Count);
        Assert.Equal(0, mid.Window[2].Distance);
        Assert.Equal("丙", mid.Window[2].Line.Translation);

        var same = stream.Tick(TimeSpan.FromSeconds(22));
        Assert.Equal(2, same.CurrentIndex);
        Assert.False(same.IndexChanged);

        var last = stream.Tick(TimeSpan.FromSeconds(41));
        Assert.Equal(4, last.CurrentIndex);
        Assert.Equal(3, last.Window.Count);
        Assert.Equal(4, last.Window[^1].Index);
    }

    [Fact]
    public void Empty_track_does_not_invent_a_line()
    {
        var stream = new SyncedLineStream();
        stream.Reset([]);
        var snap = stream.Tick(TimeSpan.FromSeconds(12));
        Assert.False(snap.HasLines);
        Assert.Empty(snap.Window);
        Assert.Equal(0, snap.CurrentIndex);
    }

    [Fact]
    public void Netease_centisecond_lrc_does_not_pin_index_at_zero()
    {
        var lines = LyricTrack.Build(
            null,
            "一\n二\n三",
            "[00:00.000] a\n[00:11.64] b\n[00:18.10] c");
        var stream = new SyncedLineStream();
        stream.Reset(lines);

        Assert.Equal(0, stream.Tick(TimeSpan.FromSeconds(1)).CurrentIndex);
        Assert.Equal(1, stream.Tick(TimeSpan.FromSeconds(12)).CurrentIndex);
        Assert.Equal(2, stream.Tick(TimeSpan.FromSeconds(19)).CurrentIndex);
    }
}
