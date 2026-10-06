using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Overlay;

namespace LyricsTranslator.Core.Tests;

public class OverlayPolicyTests
{
    [Fact]
    public void Overlay_defaults_on_when_lyrics_are_present()
    {
        Assert.True(OverlayPolicy.DefaultEnabled);
        Assert.True(OverlayPolicy.ShouldShow(overlayEnabled: true, hasLyricLines: true));
        Assert.False(OverlayPolicy.ShouldShow(overlayEnabled: false, hasLyricLines: true));
        Assert.False(OverlayPolicy.ShouldShow(overlayEnabled: true, hasLyricLines: false));
    }

    [Fact]
    public void Original_only_track_still_counts_as_overlay_lyrics()
    {
        var lines = OverlayPolicy.LinesForOverlay(
            LyricsStatus.Ready,
            original: "夜に駆ける\n君の瞳に",
            translation: null,
            syncedLrc: "[00:10.00] 夜に駆ける\n[00:20.00] 君の瞳に");
        Assert.True(OverlayPolicy.HasLyricLines(lines));
        Assert.True(OverlayPolicy.ShouldShow(OverlayPolicy.DefaultEnabled, OverlayPolicy.HasLyricLines(lines)));
        Assert.Equal(2, lines.Count);
        Assert.Equal(0, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(12)));
        Assert.Equal(1, LyricTrack.IndexAt(lines, TimeSpan.FromSeconds(21)));
    }

    [Fact]
    public void Idle_and_loading_do_not_open_overlay()
    {
        Assert.Empty(OverlayPolicy.LinesForOverlay(LyricsStatus.Idle, "a", "一", null));
        Assert.Empty(OverlayPolicy.LinesForOverlay(LyricsStatus.Loading, "a", "一", null));
        Assert.Empty(OverlayPolicy.LinesForOverlay(LyricsStatus.Instrumental, null, null, null));
    }
}
