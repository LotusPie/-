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

    [Fact]
    public void Drops_bahamut_chrome_from_overlay_translation_lines()
    {
        var lines = LyricTrack.Build(
            "a\nb\nc\nd",
            "你就像微風一般\n上一篇\n下一篇\n留言\nid=\"article_content\" class=\"text-paragraph article_container\">\n中文翻譯來源：http://b23.tv/ZCxGRfk\n闔上雙眼染上暮色\n究竟你內心在想甚麼呢\n你睜開的眼臉底下",
            "[00:00.00] a\n[00:10.00] b\n[00:20.00] c\n[00:30.00] d");

        Assert.Equal(4, lines.Count);
        Assert.Equal("你就像微風一般", lines[0].Translation);
        Assert.Equal("闔上雙眼染上暮色", lines[1].Translation);
        Assert.DoesNotContain(lines, l => l.Translation.Contains("上一篇", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Translation.Contains("留言", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Translation.Contains("article_content", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lines, l => l.Translation.Contains("b23.tv", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Drops_translator_preface_from_overlay_translation_lines()
    {
        var lines = LyricTrack.Build(
            "a\nb\nc\nd",
            "只是經常聽到他們的歌，充其量也只能算路人粉？\n還請多多包涵囉。\n啊啊 無可救藥地 在我心底蠢蠢欲動的野獸\n接連浮上水面又無疾而終的\n氣泡\n同義反覆 握緊的手裡留下氣泡\n最後也會附上我對這首歌的小小理解～",
            syncedLrc: null);

        Assert.Equal(4, lines.Count);
        Assert.Equal("啊啊 無可救藥地 在我心底蠢蠢欲動的野獸", lines[0].Translation);
        Assert.DoesNotContain(lines, l => l.Translation.Contains("路人粉", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Translation.Contains("包涵", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Translation.Contains("小小理解", StringComparison.Ordinal));
    }
}
