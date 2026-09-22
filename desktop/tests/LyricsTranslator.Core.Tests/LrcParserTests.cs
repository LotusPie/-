using LyricsTranslator.Core.Lyrics;

namespace LyricsTranslator.Core.Tests;

public class LrcParserTests
{
    [Fact]
    public void Parses_mm_ss_and_fractional_timestamps_and_sorts()
    {
        const string lrc =
            """
            [00:12.50] second
            [01:02] later
            [00:00.08] first
            [ti:ignored]
            """;
        var lines = LrcParser.Parse(lrc);
        Assert.Equal(3, lines.Count);
        Assert.Equal("first", lines[0].Text);
        Assert.Equal(TimeSpan.FromMilliseconds(80), lines[0].Timestamp);
        Assert.Equal("second", lines[1].Text);
        Assert.Equal(TimeSpan.FromSeconds(12.5), lines[1].Timestamp);
        Assert.Equal("later", lines[2].Text);
        Assert.Equal(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(2), lines[2].Timestamp);
    }

    [Fact]
    public void Empty_or_plain_text_returns_no_lines()
    {
        Assert.Empty(LrcParser.Parse(null));
        Assert.Empty(LrcParser.Parse("just plain lyrics\nno stamps"));
    }
}
