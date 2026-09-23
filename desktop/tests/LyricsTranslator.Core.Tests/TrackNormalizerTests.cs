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

    [Fact]
    public void Japanese_title_keeps_kana_and_strips_official_video()
    {
        var query = TrackNormalizer.FromRaw(
            "夜に駆ける (Official Video)",
            "YOASOBI",
            null,
            TimeSpan.FromSeconds(261),
            "Chrome",
            PlayerKind.Browser,
            true);

        Assert.Equal("夜に駆ける", query.NormalizedTitle);
        Assert.Equal("夜に駆ける", query.DisplayTitle);
        Assert.Equal("yoasobi", query.NormalizedArtist);
    }

    [Fact]
    public void Canonicalizes_yorushika_and_keeps_apple_music_english_title()
    {
        var fromEnglish = TrackNormalizer.FromRaw(
            "Sunny",
            "Yorushika",
            "second person",
            TimeSpan.FromSeconds(268),
            "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App",
            PlayerKind.AppleMusic,
            true);
        var fromJapanese = TrackNormalizer.FromRaw(
            "晴る",
            "ヨルシカ",
            "second person",
            TimeSpan.FromSeconds(268),
            "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App",
            PlayerKind.AppleMusic,
            true);

        Assert.Equal("Sunny", fromEnglish.DisplayTitle);
        Assert.Equal("Yorushika", fromEnglish.DisplayArtist);
        Assert.Equal("yorushika", fromEnglish.NormalizedArtist);
        Assert.Equal("yorushika", fromJapanese.NormalizedArtist);
        Assert.Equal("sunny", fromEnglish.NormalizedTitle);
        Assert.Contains("晴る", TitleAliases.Variants("Sunny"));
        Assert.Contains("ヨルシカ", ArtistAliases.Variants("Yorushika"));
        Assert.Contains("Sunny", TitleAliases.Variants("晴る"));
        Assert.Contains("青い栞", TitleAliases.Variants("Aoi Shiori"));
        Assert.Equal("花一匁", TrackLookup.PrimaryTitle(
            TrackNormalizer.FromRaw("Hanaichi Monnme", "BURNOUT SYNDROMES", null, null, "AppleMusic", PlayerKind.AppleMusic, true)));
    }
}
