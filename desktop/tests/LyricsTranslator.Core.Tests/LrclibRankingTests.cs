using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;

namespace LyricsTranslator.Core.Tests;

public class LrclibRankingTests
{
    [Fact]
    public void Prefers_duration_within_five_seconds()
    {
        var query = TrackNormalizer.FromRaw("Stay", "The Kid LAROI", "F*CK LOVE 3", TimeSpan.FromSeconds(141), "chrome", PlayerKind.Browser, true);
        var close = new LrclibTrack { TrackName = "Stay", ArtistName = "The Kid LAROI", Duration = 141, PlainLyrics = "a" };
        var remix = new LrclibTrack { TrackName = "Stay", ArtistName = "The Kid LAROI", Duration = 220, PlainLyrics = "b" };

        var ranked = LrclibClient.Rank([remix, close], query).ToList();
        Assert.Equal(141, ranked[0].Duration);
    }

    [Fact]
    public void Prefers_synced_lyrics_when_title_matches()
    {
        var query = TrackNormalizer.FromRaw("Stay", "The Kid LAROI", null, TimeSpan.FromSeconds(141), "chrome", PlayerKind.Browser, true);
        var plainOnly = new LrclibTrack { TrackName = "Stay", ArtistName = "The Kid LAROI", Duration = 141, PlainLyrics = "plain" };
        var withLrc = new LrclibTrack
        {
            TrackName = "Stay",
            ArtistName = "The Kid LAROI",
            Duration = 141,
            PlainLyrics = "plain",
            SyncedLyrics = "[00:01.00] plain",
        };

        var ranked = LrclibClient.Rank([plainOnly, withLrc], query).ToList();
        Assert.Equal("[00:01.00] plain", ranked[0].SyncedLyrics);
    }

    [Fact]
    public void Prefers_japanese_title_duration_and_synced_over_romaji_mismatch()
    {
        var query = TrackNormalizer.FromRaw(
            "Hanaichi Monnme",
            "BURNOUT SYNDROMES",
            null,
            TimeSpan.FromSeconds(275),
            "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App",
            PlayerKind.AppleMusic,
            true);
        var wrongRomaji = new LrclibTrack
        {
            TrackName = "hanaichimonme",
            ArtistName = "DracoVirgo",
            Duration = 203,
            SyncedLyrics = "[00:01.00] wrong",
            PlainLyrics = "wrong",
        };
        var japanese = new LrclibTrack
        {
            TrackName = "花一匁",
            ArtistName = "BURNOUT SYNDROMES",
            Duration = 275,
            SyncedLyrics = "[00:01.00] 正しい",
            PlainLyrics = "正しい",
        };
        var japaneseWrongDuration = new LrclibTrack
        {
            TrackName = "花一匁",
            ArtistName = "ずっと真夜中でいいのに。",
            Duration = 492,
            SyncedLyrics = "[00:01.00] other",
            PlainLyrics = "other",
        };

        var ranked = LrclibClient.Rank([wrongRomaji, japaneseWrongDuration, japanese], query).ToList();
        Assert.Equal("花一匁", ranked[0].TrackName);
        Assert.Equal(275.0, ranked[0].Duration);
        Assert.DoesNotContain(ranked, t => t.ArtistName == "DracoVirgo");
    }

    [Fact]
    public void Build_search_url_uses_japanese_title_not_romaji()
    {
        var romaji = TrackNormalizer.FromRaw(
            "Hanaichi Monnme",
            "BURNOUT SYNDROMES",
            null,
            TimeSpan.FromSeconds(275),
            "AppleMusic",
            PlayerKind.AppleMusic,
            true);
        var japanese = TrackNormalizer.FromRaw(
            "花一匁",
            "BURNOUT SYNDROMES",
            null,
            TimeSpan.FromSeconds(275),
            "AppleMusic",
            PlayerKind.AppleMusic,
            true);

        var romajiUrl = LrclibClient.BuildSearchUrl(romaji).ToString();
        var japaneseUrl = LrclibClient.BuildSearchUrl(japanese).ToString();
        static bool HasJapaneseTitle(string url) =>
            url.Contains("花一匁", StringComparison.Ordinal) ||
            url.Contains(Uri.EscapeDataString("花一匁"), StringComparison.Ordinal);
        Assert.True(HasJapaneseTitle(romajiUrl));
        Assert.True(HasJapaneseTitle(japaneseUrl));
        Assert.DoesNotContain("Hanaichi", romajiUrl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Hanaichi", japaneseUrl, StringComparison.OrdinalIgnoreCase);

        var urls = LrclibClient.BuildLookupUrls(romaji);
        Assert.Contains(urls, u => u.AbsoluteUri.Contains("api/get", StringComparison.Ordinal));
        Assert.All(urls.Take(2), u => Assert.True(HasJapaneseTitle(u.AbsoluteUri)));
    }

    [Fact]
    public void Rank_timed_does_not_prefer_romaji_when_japanese_original_is_known()
    {
        var query = TrackNormalizer.FromRaw(
            "Hanaichi Monnme",
            "BURNOUT SYNDROMES",
            null,
            TimeSpan.FromSeconds(275),
            "AppleMusic",
            PlayerKind.AppleMusic,
            true);
        var romaji = new LrclibTrack
        {
            TrackName = "Hanaichi Monnme",
            ArtistName = "BURNOUT SYNDROMES",
            Duration = 275,
            SyncedLyrics = "[00:01.00]abc\n[00:02.00]def\n[00:03.00]ghi",
        };
        var japanese = new LrclibTrack
        {
            TrackName = "花一匁",
            ArtistName = "BURNOUT SYNDROMES",
            Duration = 275,
            SyncedLyrics = "[00:01.00]花一匁だよ\n[00:02.00]正しい歌詞\n[00:03.00]まだ仮名",
        };

        var timed = LrclibClient.RankTimed([romaji, japanese], query).ToList();
        Assert.Equal("花一匁", timed[0].TrackName);
        Assert.DoesNotContain(timed, t => t.TrackName == "Hanaichi Monnme");
    }

    [Fact]
    public void Strips_lrc_timestamps()
    {
        var plain = LrclibClient.StripLrcTimestamps("[00:12.00]hello\n[00:15.50]world");
        Assert.Equal("hello\nworld", plain);
    }
}
