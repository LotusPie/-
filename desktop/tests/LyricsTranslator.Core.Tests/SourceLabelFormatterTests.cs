using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Pipeline;

namespace LyricsTranslator.Core.Tests;

public class SourceLabelFormatterTests
{
    [Theory]
    [InlineData(LyricsSource.Lrclib, LyricsSource.Ai, LyricsStatus.Ready, "社群／LRCLIB → AI")]
    [InlineData(LyricsSource.Paste, LyricsSource.Ai, LyricsStatus.Ready, "手貼 → AI")]
    [InlineData(LyricsSource.Lrclib, LyricsSource.Lrclib, LyricsStatus.Ready, "社群／LRCLIB")]
    [InlineData(LyricsSource.Lrclib, LyricsSource.None, LyricsStatus.Instrumental, "純音樂")]
    [InlineData(LyricsSource.Lrclib, LyricsSource.Bahamut, LyricsStatus.Ready, "社群／LRCLIB → 巴哈姆特")]
    [InlineData(LyricsSource.None, LyricsSource.Bahamut, LyricsStatus.Ready, "巴哈姆特")]
    public void Formats_labels(LyricsSource original, LyricsSource translation, LyricsStatus status, string expected)
    {
        Assert.Equal(expected, SourceLabelFormatter.Format(original, translation, status));
    }
}
