using System.Net;
using System.Text.RegularExpressions;
using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Lyrics;

/// <summary>
/// DuckDuckGo HTML search for 歌詞翻譯 pages when Bahamut misses.
/// Personal local cache only; allowlisted hosts; no lyric dump.
/// </summary>
public sealed partial class DuckDuckGoLyricsClient : IWebLyricsClient
{
    public const string UserAgent = BahamutClient.UserAgent;
    public static readonly Uri SearchEndpoint = new("https://html.duckduckgo.com/html/");

    private static readonly string[] AllowedHosts =
    [
        "home.gamer.com.tw",
        "forum.gamer.com.tw",
        "mojim.com",
        "www.mojim.com",
    ];

    private readonly HttpClient _http;

    public DuckDuckGoLyricsClient(HttpClient http)
    {
        _http = http;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }

        if (!_http.DefaultRequestHeaders.AcceptLanguage.Any())
        {
            _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-TW,zh;q=0.9,en;q=0.6");
        }
    }

    public async Task<CommunityTranslation?> FindAsync(TrackQuery query, CancellationToken cancellationToken)
    {
        if (!query.HasIdentity || !BahamutParser.ShouldSearch(query))
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        var keywords = BahamutParser.BuildSearchQueries(query)
            .Where(static q => q.Contains("歌詞", StringComparison.Ordinal) || q.Contains("翻譯", StringComparison.Ordinal))
            .Take(8)
            .ToList();

        var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var keyword in keywords)
        {
            IReadOnlyList<WebHit> hits;
            try
            {
                var html = await _http.GetStringAsync(BuildSearchUrl(keyword), timeout.Token).ConfigureAwait(false);
                hits = ParseSearchHits(html);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (HttpRequestException)
            {
                continue;
            }

            foreach (var hit in Rank(hits, query).Take(5))
            {
                if (!tried.Add(hit.Url.AbsoluteUri))
                {
                    continue;
                }

                var found = await TryFetchAsync(hit, timeout.Token).ConfigureAwait(false);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    public static Uri BuildSearchUrl(string keyword)
    {
        var builder = new UriBuilder(SearchEndpoint)
        {
            Query = "q=" + Uri.EscapeDataString(keyword),
        };
        return builder.Uri;
    }

    public static IReadOnlyList<WebHit> ParseSearchHits(string html)
    {
        var hits = new List<WebHit>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ResultLinkRegex().Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
            var title = WebUtility.HtmlDecode(match.Groups["title"].Value).Trim();
            var url = Unwrap(href);
            if (url is null || title.Length == 0 || !IsAllowed(url) || !seen.Add(url.AbsoluteUri))
            {
                continue;
            }

            hits.Add(new WebHit(title, url));
        }

        return hits;
    }

    public static IEnumerable<WebHit> Rank(IEnumerable<WebHit> hits, TrackQuery query) =>
        hits
            .Select(h => (Hit: h, Score: BahamutParser.ScoreHit(new BahamutSearchHit("0", h.Title, h.Url.AbsoluteUri), query)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Hit);

    public static bool IsAllowed(Uri url)
    {
        var host = url.Host;
        return AllowedHosts.Any(allowed =>
            host.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase));
    }

    public sealed record WebHit(string Title, Uri Url);

    private async Task<CommunityTranslation?> TryFetchAsync(WebHit hit, CancellationToken cancellationToken)
    {
        try
        {
            var html = await _http.GetStringAsync(hit.Url, cancellationToken).ConfigureAwait(false);
            var text = BahamutParser.ExtractArticleText(html);
            var lyrics = BahamutParser.ExtractTraditionalChineseLyrics(text);
            if (string.IsNullOrWhiteSpace(lyrics))
            {
                return null;
            }

            var site = BahamutParser.SiteLabelFromUrl(hit.Url.AbsoluteUri);
            return new CommunityTranslation(lyrics, hit.Title, hit.Url.AbsoluteUri, site);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    internal static Uri? Unwrap(string href)
    {
        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
        {
            return null;
        }

        const string marker = "uddg=";
        var query = uri.Query;
        var start = query.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            var rest = query[(start + marker.Length)..];
            var amp = rest.IndexOf('&');
            if (amp >= 0)
            {
                rest = rest[..amp];
            }

            var decoded = Uri.UnescapeDataString(rest);
            if (Uri.TryCreate(decoded, UriKind.Absolute, out var inner))
            {
                return inner;
            }
        }

        return uri;
    }

    [GeneratedRegex(
        @"<a[^>]*class=""[^""]*result__a[^""]*""[^>]*href=""(?<href>[^""]+)""[^>]*>(?<title>[\s\S]{1,240}?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ResultLinkRegex();
}
