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
    public void Strips_lrc_timestamps()
    {
        var plain = LrclibClient.StripLrcTimestamps("[00:12.00]hello\n[00:15.50]world");
        Assert.Equal("hello\nworld", plain);
    }
}
