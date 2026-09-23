using LyricsTranslator.Core.Lyrics;

namespace LyricsTranslator.Core.Tests;

public class LyricTrackTests
{
    [Fact]
    public void Index_follows_lrc_timestamps()
    {
        var lines = LyricTrack.Build(
            "a\nb\nc",
            "甲\n乙\n丙",
            "[00:00.00] a\n[00:10.00] b\n[00:20.00] c");

        Assert.Equal(3, lines.Count);
        Assert.Equal(TimeSpan.FromSeconds(10), lines[1].Timestamp);
        Assert.Equal(0, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(0)));
        Assert.Equal(0, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(9.9)));
        Assert.Equal(1, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(10)));
        Assert.Equal(2, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(25)));
        Assert.Equal("乙", lines[1].Translation);
        Assert.Equal("b", lines[1].Original);
    }

    [Fact]
    public void Maps_timed_original_onto_fewer_translation_lines()
    {
        var lines = LyricTrack.Build(
            "a\nb\nc",
            "甲\n乙",
            "[00:00.00] a\n[00:10.00] b\n[00:20.00] c");

        Assert.Equal(2, lines.Count);
        Assert.Equal("甲", lines[0].Translation);
        Assert.Equal("乙", lines[1].Translation);
        Assert.Equal(TimeSpan.Zero, lines[0].Timestamp);
        Assert.Equal(TimeSpan.FromSeconds(20), lines[1].Timestamp);
        Assert.Equal("a", lines[0].Original);
        Assert.Equal("c", lines[1].Original);
        Assert.Equal(0, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(9)));
        Assert.Equal(0, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(10)));
        Assert.Equal(1, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(20)));
        Assert.Equal(1, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(29)));
    }

    [Fact]
    public void Maps_timed_original_onto_more_translation_lines()
    {
        var lines = LyricTrack.Build(
            "a\nb\nc",
            "甲\n乙\n丙\n丁\n戊",
            "[00:00.00] a\n[00:10.00] b\n[00:20.00] c");

        Assert.Equal(5, lines.Count);
        Assert.Equal(TimeSpan.Zero, lines[0].Timestamp);
        Assert.Equal(TimeSpan.FromSeconds(5), lines[1].Timestamp);
        Assert.Equal(TimeSpan.FromSeconds(10), lines[2].Timestamp);
        Assert.Equal(TimeSpan.FromSeconds(15), lines[3].Timestamp);
        Assert.Equal(TimeSpan.FromSeconds(20), lines[4].Timestamp);
        Assert.Equal("甲", lines[0].Translation);
        Assert.Equal("戊", lines[4].Translation);
        Assert.Equal("a", lines[0].Original);
        Assert.Equal("b", lines[2].Original);
        Assert.Equal("c", lines[4].Original);
        Assert.Equal(0, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(0)));
        Assert.Equal(1, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(6)));
        Assert.Equal(2, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(12)));
        Assert.Equal(3, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(16)));
        Assert.Equal(4, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(20)));
    }

    [Fact]
    public void Without_lrc_stays_on_first_line_instead_of_duration_ratio()
    {
        var lines = LyricTrack.Build("one\ntwo\nthree\nfour", "一\n二\n三\n四", syncedLrc: null);
        Assert.Equal(4, lines.Count);
        Assert.All(lines, line => Assert.Null(line.Timestamp));
        Assert.Equal(0, LyricTrack.IndexAt(lines, TimeSpan.Zero));
        Assert.Equal(0, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(30)));
        Assert.Equal(0, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(99)));
    }

    [Fact]
    public void Without_duration_stays_on_first_line()
    {
        var lines = LyricTrack.Build("one\ntwo", "一\n二", syncedLrc: null);
        Assert.Equal(0, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(40)));
    }
}
