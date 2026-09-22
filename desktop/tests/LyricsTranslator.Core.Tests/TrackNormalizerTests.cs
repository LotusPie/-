using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;

namespace LyricsTranslator.Core.Tests;

public class TrackNormalizerTests
{
    [Fact]
    public void Splits_apple_music_artist_em_dash_album()
    {
        var query = TrackNormalizer.FromRaw(
            title: "Anti-Hero",
            artist: "Taylor Swift — Midnights",
            album: null,
            duration: TimeSpan.FromSeconds(201),
            sourceAppId: "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App",
            playerKind: PlayerKind.AppleMusic,
            isPlaying: true);

        Assert.Equal("taylor swift", query.NormalizedArtist);
        Assert.Equal("Midnights", query.Album);
        Assert.Equal("anti-hero", query.NormalizedTitle);
    }

    [Fact]
    public void Strips_youtube_official_video_and_parses_artist_from_title()
    {
        var query = TrackNormalizer.FromRaw(
            title: "NewJeans - Super Shy (Official Video)",
            artist: "NewJeans Official",
            album: null,
            duration: TimeSpan.FromSeconds(195),
            sourceAppId: "Chrome",
            playerKind: PlayerKind.Browser,
            isPlaying: true);

        Assert.Equal("super shy", query.NormalizedTitle);
        Assert.Equal("newjeans", query.NormalizedArtist);
    }

    [Fact]
    public void Strips_feat_and_topic_suffix()
    {
        var query = TrackNormalizer.FromRaw(
            title: "Stay (Official Lyric Video)",
            artist: "The Kid LAROI - Topic",
            album: null,
            duration: null,
            sourceAppId: "msedge",
            playerKind: PlayerKind.Browser,
            isPlaying: true);

        Assert.Equal("stay", query.NormalizedTitle);
        Assert.DoesNotContain("topic", query.NormalizedArtist);
    }

    [Fact]
    public void Cache_key_is_stable_for_fullwidth_and_case()
    {
        var a = TrackNormalizer.FromRaw("HELLO", "Ａｒｔｉｓｔ", null, null, "Chrome", PlayerKind.Browser, true);
        var b = TrackNormalizer.FromRaw("hello", "artist", null, null, "Chrome", PlayerKind.Browser, true);
        Assert.Equal(a.CacheKey, b.CacheKey);
    }

    [Fact]
    public void Canonicalizes_bts_aliases()
    {
        var query = TrackNormalizer.FromRaw("Dynamite", "방탄소년단", null, null, "chrome", PlayerKind.Browser, true);
        Assert.Equal("bts", query.NormalizedArtist);
    }
}
