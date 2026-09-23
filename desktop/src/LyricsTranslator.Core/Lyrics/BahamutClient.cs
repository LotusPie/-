using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Lyrics;

public sealed class BahamutClient : IBahamutClient
{
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";

    private readonly HttpClient _http;

    public BahamutClient(HttpClient http)
    {
        _http = http;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }

        if (!_http.DefaultRequestHeaders.AcceptLanguage.Any())
        {
            _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-TW,zh;q=0.9");
        }
    }

    public async Task<CommunityTranslation?> FindAsync(TrackQuery query, CancellationToken cancellationToken)
    {
        if (!query.HasIdentity || !BahamutParser.ShouldSearch(query))
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        var tried = new HashSet<string>(StringComparer.Ordinal);
        foreach (var keyword in BahamutParser.BuildSearchQueries(query))
        {
            CommunityTranslation? hit;
            try
            {
                hit = await SearchOnceAsync(query, keyword, tried, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (HttpRequestException)
            {
                continue;
            }

            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    private async Task<CommunityTranslation?> SearchOnceAsync(
        TrackQuery query,
        string keyword,
        HashSet<string> tried,
        CancellationToken cancellationToken)
    {
        string html;
        try
        {
            html = await _http.GetStringAsync(BahamutParser.BuildSearchUrl(keyword), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }

        var ranked = BahamutParser.ParseSearchHits(html)
            .Select(h => (Hit: h, Score: BahamutParser.ScoreHit(h, query, keyword)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        foreach (var (hit, _) in ranked.Take(5))
        {
            if (!tried.Add(hit.Sn))
            {
                continue;
            }

            var translation = await TryFetchAsync(hit, cancellationToken).ConfigureAwait(false);
            if (translation is not null)
            {
                return translation;
            }
        }

        return null;
    }

    private async Task<CommunityTranslation?> TryFetchAsync(BahamutSearchHit hit, CancellationToken cancellationToken)
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

            return new CommunityTranslation(lyrics, hit.Title, hit.Url, "巴哈姆特");
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
}
