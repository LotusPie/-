using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;

namespace LyricsTranslator.Core.Lyrics;

/// <summary>
/// Personal-use timed lyrics from NetEase Cloud Music. Japanese/Korean catalogues
/// store real <c>[mm:ss.xx]</c> LRC (the <c>lrc</c> field, never <c>tlyric</c>).
/// </summary>
public sealed class NeteaseTimedLyricsClient : ITimedLyricsSource
{
    public const string NameValue = "NetEase";
    public const int DurationToleranceSeconds = 5;

    private static readonly Uri SearchUri = new("https://music.163.com/api/search/get");
    private static readonly Uri LyricUri = new("https://music.163.com/api/song/lyric");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    public NeteaseTimedLyricsClient(HttpClient http)
    {
        _http = http;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(BahamutClient.UserAgent);
        }

        if (!_http.DefaultRequestHeaders.Referrer?.ToString().Contains("music.163.com", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            _http.DefaultRequestHeaders.Referrer = new Uri("https://music.163.com/");
        }

        if (!_http.DefaultRequestHeaders.Accept.Any())
        {
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    public string Name => NameValue;

    public async Task<TimedLyricsHit?> FindAsync(TrackQuery query, LyricLanguage language, CancellationToken cancellationToken)
    {
        if (!query.HasIdentity ||
            language is not (LyricLanguage.Japanese or LyricLanguage.Korean))
        {
            return null;
        }

        var songs = await SearchSongsAsync(query, cancellationToken).ConfigureAwait(false);
        foreach (var song in Rank(songs, query, language).Take(4))
        {
            var lrc = await FetchLyricAsync(song.Id, cancellationToken).ConfigureAwait(false);
            var cleaned = LrcLanguageFilter.Clean(lrc);
            if (cleaned is null || !LrcLanguageFilter.Fits(cleaned, language))
            {
                continue;
            }

            return new TimedLyricsHit(
                cleaned,
                NameValue,
                song.Name,
                song.ArtistName,
                song.DurationSeconds);
        }

        return null;
    }

    public static IReadOnlyList<string> BuildSearchQueries(TrackQuery query)
    {
        var titles = TrackLookup.Titles(query);
        var artists = TrackLookup.Artists(query);
        var artist = artists.FirstOrDefault() ?? string.Empty;
        var queries = new List<string>();
        foreach (var title in titles.Take(4))
        {
            Add(queries, title);
            if (artist.Length > 0)
            {
                Add(queries, $"{title} {artist}");
            }
        }

        return queries;
    }

    public static Uri BuildSearchUrl(string keyword)
    {
        var builder = new UriBuilder(SearchUri)
        {
            Query = "s=" + Uri.EscapeDataString(keyword) + "&type=1&offset=0&limit=8",
        };
        return builder.Uri;
    }

    public static IEnumerable<NeteaseSong> Rank(
        IEnumerable<NeteaseSong> songs,
        TrackQuery query,
        LyricLanguage language)
    {
        return songs
            .Select(s => (Song: s, Score: Score(s, query, language)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Song);
    }

    public static int Score(NeteaseSong song, TrackQuery query, LyricLanguage language)
    {
        var title = TrackNormalizer.NormalizeToken(TrackNormalizer.StripTitleNoise(song.Name ?? string.Empty));
        var artist = TrackNormalizer.NormalizeToken(TrackNormalizer.StripArtistNoise(song.ArtistName ?? string.Empty));
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

        if (language == LyricLanguage.Japanese &&
            !LanguageDetector.LooksLikeJapaneseOrKanjiTitle(song.Name) &&
            !LanguageDetector.LooksLikeJapanese(song.Name))
        {
            return 0;
        }

        if (language == LyricLanguage.Korean && !LanguageDetector.LooksLikeKorean(song.Name))
        {
            return 0;
        }

        var score = 0;
        if (titleVariants.Any(variant => title == variant))
        {
            score += 100;
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

        if (artistVariants.Any(variant => artist == variant))
        {
            score += 80;
        }
        else if (artistVariants.Any(variant =>
                     artist.Contains(variant, StringComparison.Ordinal) ||
                     variant.Contains(artist, StringComparison.Ordinal)))
        {
            score += 30;
        }

        if (query.Duration is { } duration && song.DurationSeconds > 0)
        {
            var delta = Math.Abs(duration.TotalSeconds - song.DurationSeconds);
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

        return score;
    }

    private async Task<IReadOnlyList<NeteaseSong>> SearchSongsAsync(TrackQuery query, CancellationToken cancellationToken)
    {
        var merged = new Dictionary<long, NeteaseSong>();
        foreach (var keyword in BuildSearchQueries(query))
        {
            SearchResponse? parsed;
            try
            {
                var json = await _http.GetStringAsync(BuildSearchUrl(keyword), cancellationToken).ConfigureAwait(false);
                parsed = JsonSerializer.Deserialize<SearchResponse>(json, JsonOptions);
            }
            catch (HttpRequestException)
            {
                continue;
            }
            catch (JsonException)
            {
                continue;
            }

            foreach (var song in parsed?.Result?.Songs ?? [])
            {
                if (song.Id != 0)
                {
                    merged[song.Id] = song;
                }
            }

            if (merged.Count >= 8)
            {
                break;
            }
        }

        return merged.Values.ToList();
    }

    private async Task<string?> FetchLyricAsync(long id, CancellationToken cancellationToken)
    {
        try
        {
            var url = new UriBuilder(LyricUri)
            {
                Query = $"id={id}&lv=1&kv=1&tv=-1",
            }.Uri;
            var json = await _http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            var parsed = JsonSerializer.Deserialize<LyricResponse>(json, JsonOptions);
            if (parsed is null || parsed.Nolyric || parsed.Uncollected)
            {
                return null;
            }

            return parsed.Lrc?.Lyric;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Add(List<string> queries, string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || queries.Contains(trimmed, StringComparer.Ordinal))
        {
            return;
        }

        queries.Add(trimmed);
    }

    public sealed class NeteaseSong
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("duration")]
        public long Duration { get; set; }

        [JsonPropertyName("dt")]
        public long Dt { get; set; }

        [JsonPropertyName("artists")]
        public List<NeteaseArtist>? Artists { get; set; }

        public string? ArtistName => Artists?.FirstOrDefault()?.Name;

        public double DurationSeconds
        {
            get
            {
                var ms = Duration > 0 ? Duration : Dt;
                return ms > 0 ? ms / 1000.0 : 0;
            }
        }
    }

    public sealed class NeteaseArtist
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class SearchResponse
    {
        [JsonPropertyName("result")]
        public SearchResult? Result { get; set; }
    }

    private sealed class SearchResult
    {
        [JsonPropertyName("songs")]
        public List<NeteaseSong>? Songs { get; set; }
    }

    private sealed class LyricResponse
    {
        [JsonPropertyName("nolyric")]
        public bool Nolyric { get; set; }

        [JsonPropertyName("uncollected")]
        public bool Uncollected { get; set; }

        [JsonPropertyName("lrc")]
        public LyricBody? Lrc { get; set; }
    }

    private sealed class LyricBody
    {
        [JsonPropertyName("lyric")]
        public string? Lyric { get; set; }
    }
}
