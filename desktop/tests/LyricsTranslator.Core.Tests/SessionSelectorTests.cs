using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.NowPlaying;

namespace LyricsTranslator.Core.Tests;

public class SessionSelectorTests
{
    [Fact]
    public void Classifies_apple_music_and_chrome()
    {
        Assert.Equal(PlayerKind.AppleMusic, SourceAppClassifier.Classify("AppleInc.AppleMusicWin_nzyj5cx40ttqa!App"));
        Assert.Equal(PlayerKind.Browser, SourceAppClassifier.Classify("Chrome"));
        Assert.Equal(PlayerKind.Browser, SourceAppClassifier.Classify("MSEdge"));
        Assert.Equal(PlayerKind.Other, SourceAppClassifier.Classify("Spotify.exe"));
        Assert.Equal(PlayerKind.Other, SourceAppClassifier.Classify("AppleInc.iTunes_..."));
    }

    [Fact]
    public void Auto_prefers_playing_apple_music_over_browser()
    {
        var sessions = new[]
        {
            Browser("random video", isPlaying: true, duration: TimeSpan.FromMinutes(3)),
            Apple("Anti-Hero", isPlaying: true),
        };

        var picked = SessionSelector.Pick(sessions, PlayerPin.Auto, detectionPaused: false);
        Assert.Equal("Anti-Hero", picked?.Title);
    }

    [Fact]
    public void Pin_youtube_music_ignores_apple_music()
    {
        var sessions = new[]
        {
            Apple("Anti-Hero", isPlaying: true),
            Browser("NewJeans - Super Shy", isPlaying: true, duration: TimeSpan.FromMinutes(3)),
        };

        var picked = SessionSelector.Pick(sessions, PlayerPin.YouTubeMusic, detectionPaused: false);
        Assert.Contains("Super Shy", picked?.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Skips_spotify_and_long_browser_videos()
    {
        var sessions = new[]
        {
            new NowPlayingSession("Spotify.exe", "Song", "Artist", null, TimeSpan.FromMinutes(3), true, PlayerKind.Other),
            Browser("Very Long Documentary", isPlaying: true, duration: TimeSpan.FromHours(2)),
        };

        Assert.Null(SessionSelector.Pick(sessions, PlayerPin.Auto, detectionPaused: false));
    }

    [Fact]
    public void Paused_returns_null()
    {
        var sessions = new[] { Apple("Anti-Hero", isPlaying: true) };
        Assert.Null(SessionSelector.Pick(sessions, PlayerPin.Auto, detectionPaused: true));
    }

    [Fact]
    public void Apple_music_with_metadata_is_kept_even_if_not_playing()
    {
        var sessions = new[] { Apple("Anti-Hero", isPlaying: false) };
        var picked = SessionSelector.Pick(sessions, PlayerPin.AppleMusic, detectionPaused: false);
        Assert.Equal("Anti-Hero", picked?.Title);
    }

    private static NowPlayingSession Apple(string title, bool isPlaying) =>
        new("AppleInc.AppleMusicWin_nzyj5cx40ttqa!App", title, "Taylor Swift — Midnights", null, TimeSpan.FromSeconds(200), isPlaying, PlayerKind.AppleMusic);

    private static NowPlayingSession Browser(string title, bool isPlaying, TimeSpan duration) =>
        new("Chrome", title, "VEVO", null, duration, isPlaying, PlayerKind.Browser);
}
