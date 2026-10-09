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

    [Fact]
    public void Parses_netease_three_decimal_and_colon_fraction_without_pinning_t0()
    {
        const string lrc =
            """
            [ti:あぶく]
            [ar:ヨルシカ]
            [offset:0]
            [00:00.000] 作词 : n-buna
            [00:01.000] 作曲 : n-buna
            [00:11.641]あぁどうしようもないほどに
            [00:14.407]私に蠢く獣
            [00:18.106]水面浮かんで浮かんでは消えるあぶく
            [00:25.061]
            [00:29.247]あぁどうしようもなく悲しい
            [01:05.020]想像は少しの泡銭
            [00:11:64]colon hundredths
            """;

        var lines = LrcParser.Parse(lrc);
        Assert.Equal(6, lines.Count);
        Assert.DoesNotContain(lines, l => l.Text.Contains("作词", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Text.Contains("作曲", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Text == "colon hundredths" && l.Timestamp == TimeSpan.FromSeconds(11) + TimeSpan.FromMilliseconds(640));
        var firstLyric = lines.Single(l => l.Text == "あぁどうしようもないほどに");
        Assert.Equal(TimeSpan.FromSeconds(11) + TimeSpan.FromMilliseconds(641), firstLyric.Timestamp);
        Assert.NotEqual(TimeSpan.Zero, firstLyric.Timestamp);
        Assert.Equal(TimeSpan.FromSeconds(14) + TimeSpan.FromMilliseconds(407), lines.Single(l => l.Text == "私に蠢く獣").Timestamp);
        Assert.Equal(
            TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(5) + TimeSpan.FromMilliseconds(20),
            lines.Single(l => l.Text == "想像は少しの泡銭").Timestamp);
        Assert.True(lines.All(l => l.Timestamp > TimeSpan.Zero));
        Assert.True(lines.DistinctBy(l => l.Timestamp).Count() >= 5);
    }
}
