using System.Text.Json.Serialization;

namespace LyricsTranslator.Core.Lyrics;

public sealed class LrclibTrack
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("trackName")]
    public string? TrackName { get; set; }

    [JsonPropertyName("artistName")]
    public string? ArtistName { get; set; }

    [JsonPropertyName("albumName")]
    public string? AlbumName { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("instrumental")]
    public bool Instrumental { get; set; }

    [JsonPropertyName("plainLyrics")]
    public string? PlainLyrics { get; set; }

    [JsonPropertyName("syncedLyrics")]
    public string? SyncedLyrics { get; set; }

    public string? EffectivePlainLyrics
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(PlainLyrics))
            {
                return PlainLyrics;
            }

            return LrclibClient.StripLrcTimestamps(SyncedLyrics);
        }
    }
}
