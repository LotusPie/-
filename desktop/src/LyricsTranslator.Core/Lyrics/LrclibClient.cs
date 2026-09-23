using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;

namespace LyricsTranslator.Core.Lyrics;

public sealed partial class LrclibClient : ILrclibClient
{
    public const string UserAgent = "LyricsTranslator/1.0 (https://github.com/LotusPie/-; personal local tool)";
    public const int DurationToleranceSeconds = 5;

    private static readonly Uri SearchUri = new("https://lrclib.net/api/search");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    public LrclibClient(HttpClient http)
    {
        _http = http;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }

        if (!_http.DefaultRequestHeaders.Accept.Any())
        {
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    public async Task<LrclibTrack?> FindAsync(TrackQuery query, CancellationToken cancellationToken)
    {
        if (!query.HasIdentity)
        {
            return null;
        }

        var results = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        return Rank(results, query).FirstOrDefault(t =>
            t.Instrumental ||
            !string.IsNullOrWhiteSpace(t.EffectivePlainLyrics) ||
            !string.IsNullOrWhiteSpace(t.SyncedLyrics));
    }

    public async Task<IReadOnlyList<LrclibTrack>> SearchAsync(TrackQuery query, CancellationToken cancellationToken)
    {
        var url = BuildSearchUrl(query);
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var tracks = await JsonSerializer.DeserializeAsync<List<LrclibTrack>>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return tracks ?? [];
    }

    public static Uri BuildSearchUrl(TrackQuery query)
    {
        var builder = new UriBuilder(SearchUri)
        {
            Query = $"track_name={Uri.EscapeDataString(query.DisplayTitle)}" +
                    (string.IsNullOrWhiteSpace(query.DisplayArtist)
                        ? string.Empty
                        : $"&artist_name={Uri.EscapeDataString(query.DisplayArtist)}"),
        };
        return builder.Uri;
    }

    public static IEnumerable<LrclibTrack> Rank(IEnumerable<LrclibTrack> tracks, TrackQuery query)
    {
        return tracks
            .Select(t => (Track: t, Score: Score(t, query)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Track);
    }

    public static int Score(LrclibTrack track, TrackQuery query)
    {
        var title = TrackNormalizer.NormalizeToken(TrackNormalizer.StripTitleNoise(track.TrackName ?? string.Empty));
        var artist = TrackNormalizer.NormalizeToken(TrackNormalizer.StripArtistNoise(track.ArtistName ?? string.Empty));
        artist = ArtistAliases.Canonicalize(artist);

        if (title.Length == 0)
        {
            return 0;
        }

        var score = 0;
        if (title == query.NormalizedTitle)
        {
            score += 100;
        }
        else if (title.Contains(query.NormalizedTitle, StringComparison.Ordinal) ||
                 query.NormalizedTitle.Contains(title, StringComparison.Ordinal))
        {
            score += 40;
        }
        else
        {
            return 0;
        }

        if (!string.IsNullOrEmpty(query.NormalizedArtist) && artist == query.NormalizedArtist)
        {
            score += 80;
        }
        else if (!string.IsNullOrEmpty(query.NormalizedArtist) &&
                 (artist.Contains(query.NormalizedArtist, StringComparison.Ordinal) ||
                  query.NormalizedArtist.Contains(artist, StringComparison.Ordinal)))
        {
            score += 30;
        }

        if (query.Duration is { } duration && track.Duration > 0)
        {
            var delta = Math.Abs(duration.TotalSeconds - track.Duration);
            if (delta <= 2)
            {
                score += 25;
            }
            else if (delta <= DurationToleranceSeconds)
            {
                score += 10;
            }
            else
            {
                score -= 20;
            }
        }

        if (!string.IsNullOrWhiteSpace(track.EffectivePlainLyrics))
        {
            score += 5;
        }

        if (!string.IsNullOrWhiteSpace(track.SyncedLyrics))
        {
            score += 15;
        }

        return score;
    }

    public static string? StripLrcTimestamps(string? synced)
    {
        if (string.IsNullOrWhiteSpace(synced))
        {
            return null;
        }

        var stripped = TimestampRegex().Replace(synced, string.Empty);
        var lines = stripped.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(static line => line.TrimEnd());
        return string.Join('\n', lines).Trim();
    }

    [GeneratedRegex(@"\[(?:\d{1,2}:)?\d{1,2}:\d{2}(?:\.\d{1,3})?\]", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampRegex();
}
