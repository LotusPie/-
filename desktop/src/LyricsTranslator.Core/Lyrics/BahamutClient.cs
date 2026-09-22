using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Lyrics;

public sealed class BahamutClient : IBahamutClient
{
    public const string UserAgent = LrclibClient.UserAgent;

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
        timeout.CancelAfter(TimeSpan.FromSeconds(8));

        string html;
        try
        {
            html = await _http.GetStringAsync(BahamutParser.BuildSearchUrl(query), timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }

        var ranked = BahamutParser.ParseSearchHits(html)
            .Select(h => (Hit: h, Score: BahamutParser.ScoreHit(h, query)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(3)
            .ToList();

        foreach (var (hit, _) in ranked)
        {
            var translation = await TryFetchAsync(hit, timeout.Token).ConfigureAwait(false);
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

            return new CommunityTranslation(lyrics, hit.Title, hit.Url);
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
