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
    private static readonly Uri GetUri = new("https://lrclib.net/api/get");
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
        var merged = new Dictionary<long, LrclibTrack>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in BuildLookupUrls(query))
        {
            if (!seenUrls.Add(url.AbsoluteUri))
            {
                continue;
            }

            IReadOnlyList<LrclibTrack> batch;
            try
            {
                batch = await FetchAsync(url, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                continue;
            }
            catch (JsonException)
            {
                continue;
            }

            foreach (var track in batch)
            {
                if (track.Id != 0)
                {
                    merged[track.Id] = track;
                }
                else
                {
                    merged[merged.Count + 1] = track;
                }
            }

            if (merged.Values.Any(t =>
                    !string.IsNullOrWhiteSpace(t.SyncedLyrics) &&
                    Score(t, query) >= 200))
            {
                break;
            }
        }

        return merged.Values.ToList();
    }

    public static Uri BuildSearchUrl(TrackQuery query)
    {
        var title = TrackLookup.PrimaryTitle(query);
        var artist = TrackLookup.PrimaryArtist(query);
        var builder = new UriBuilder(SearchUri)
        {
            Query = $"track_name={Uri.EscapeDataString(title)}" +
                    (string.IsNullOrWhiteSpace(artist)
                        ? string.Empty
                        : $"&artist_name={Uri.EscapeDataString(artist)}"),
        };
        return builder.Uri;
    }

    public static Uri BuildGetUrl(TrackQuery query)
    {
        var title = TrackLookup.PrimaryTitle(query);
        var artist = TrackLookup.PrimaryArtist(query);
        var parts = new List<string>
        {
            "track_name=" + Uri.EscapeDataString(title),
        };
        if (!string.IsNullOrWhiteSpace(artist))
        {
            parts.Add("artist_name=" + Uri.EscapeDataString(artist));
        }

        if (!string.IsNullOrWhiteSpace(query.Album))
        {
            parts.Add("album_name=" + Uri.EscapeDataString(query.Album));
        }

        if (query.Duration is { TotalSeconds: > 0 } duration)
        {
            parts.Add("duration=" + Math.Round(duration.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return new UriBuilder(GetUri) { Query = string.Join('&', parts) }.Uri;
    }

    public static IReadOnlyList<Uri> BuildLookupUrls(TrackQuery query)
    {
        var urls = new List<Uri>();
        var titles = TrackLookup.Titles(query);
        var artists = TrackLookup.Artists(query);
        var primaryArtist = artists.FirstOrDefault() ?? string.Empty;

        void Add(Uri uri)
        {
            if (urls.All(existing => !string.Equals(existing.AbsoluteUri, uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase)))
            {
                urls.Add(uri);
            }
        }

        if (query.Duration is { TotalSeconds: > 0 })
        {
            Add(BuildGetUrl(query));
        }

        Add(BuildSearchUrl(query));

        foreach (var title in titles.Take(3))
        {
            Add(new UriBuilder(SearchUri) { Query = "q=" + Uri.EscapeDataString(title) }.Uri);
            if (primaryArtist.Length > 0)
            {
                Add(new UriBuilder(SearchUri)
                {
                    Query = "track_name=" + Uri.EscapeDataString(title) +
                            "&artist_name=" + Uri.EscapeDataString(primaryArtist),
                }.Uri);
            }
        }

        return urls.Take(6).ToList();
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
        var titleVariants = TrackLookup.Titles(query)
            .Select(value => TrackNormalizer.NormalizeToken(TrackNormalizer.StripTitleNoise(value)))
            .Where(static value => value.Length >= 1)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var artistVariants = TrackLookup.Artists(query)
            .Select(value => ArtistAliases.Canonicalize(TrackNormalizer.NormalizeToken(TrackNormalizer.StripArtistNoise(value))))
            .Where(static value => value.Length >= 1)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (title.Length == 0 || titleVariants.Count == 0)
        {
            return 0;
        }

        var nativeTitleExpected = TrackLookup.Titles(query)
            .Any(LanguageDetector.LooksLikeJapaneseOrKanjiTitle);
        var trackTitleIsNative = LanguageDetector.LooksLikeJapaneseOrKanjiTitle(track.TrackName);

        var score = 0;
        if (titleVariants.Any(variant => title == variant))
        {
            score += 100;
            if (trackTitleIsNative)
            {
                score += 40;
            }
        }
        else if (titleVariants.Any(variant =>
                     title.Contains(variant, StringComparison.Ordinal) ||
                     variant.Contains(title, StringComparison.Ordinal)))
        {
            score += 40;
        }
        else
        {
            return 0;
        }

        var artistExact = artistVariants.Count > 0 && artistVariants.Any(variant => artist == variant);
        var artistPartial = artistVariants.Count > 0 &&
                            artistVariants.Any(variant =>
                                artist.Contains(variant, StringComparison.Ordinal) ||
                                variant.Contains(artist, StringComparison.Ordinal));
        if (artistExact)
        {
            score += 80;
        }
        else if (artistPartial)
        {
            score += 30;
        }

        var durationDelta = query.Duration is { } duration && track.Duration > 0
            ? Math.Abs(duration.TotalSeconds - track.Duration)
            : (double?)null;
        if (durationDelta is { } delta)
        {
            if (delta <= 2)
            {
                score += 40;
            }
            else if (delta <= DurationToleranceSeconds)
            {
                score += 20;
            }
            else
            {
                score -= 20;
            }
        }

        if (nativeTitleExpected && !trackTitleIsNative)
        {
            var durationOk = durationDelta is { } d && d <= DurationToleranceSeconds;
            if (!artistExact || !durationOk)
            {
                return 0;
            }

            score -= 50;
        }

        if (!string.IsNullOrWhiteSpace(track.EffectivePlainLyrics))
        {
            score += 5;
        }

        if (!string.IsNullOrWhiteSpace(track.SyncedLyrics))
        {
            score += 35;
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

    private async Task<IReadOnlyList<LrclibTrack>> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var trimmed = json.TrimStart();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return JsonSerializer.Deserialize<List<LrclibTrack>>(json, JsonOptions) ?? [];
        }

        var one = JsonSerializer.Deserialize<LrclibTrack>(json, JsonOptions);
        return one is null ? [] : [one];
    }

    [GeneratedRegex(@"\[(?:\d{1,2}:)?\d{1,2}:\d{2}(?:\.\d{1,3})?\]", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampRegex();
}
