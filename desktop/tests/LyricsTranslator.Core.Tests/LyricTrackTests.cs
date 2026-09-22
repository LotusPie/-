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

        Assert.Equal(0, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(30)));
        Assert.Equal(1, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)));
        Assert.Equal(2, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(30)));
        Assert.Equal("乙", lines[1].Translation);
    }

    [Fact]
    public void Without_lrc_estimates_from_duration_ratio()
    {
        var lines = LyricTrack.Build("one\ntwo\nthree\nfour", "一\n二\n三\n四", syncedLrc: null);
        Assert.All(lines, line => Assert.Null(line.Timestamp));
        Assert.Equal(0, LyricTrack.IndexAt(lines, TimeSpan.Zero, TimeSpan.FromSeconds(100)));
        Assert.Equal(1, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(100)));
        Assert.Equal(3, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(99), TimeSpan.FromSeconds(100)));
    }

    [Fact]
    public void Without_duration_stays_on_first_line()
    {
        var lines = LyricTrack.Build("one\ntwo", "一\n二", syncedLrc: null);
        Assert.Equal(0, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(40), duration: null));
    }
}
