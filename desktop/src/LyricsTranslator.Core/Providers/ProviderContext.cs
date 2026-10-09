using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Providers;

/// <summary>
/// Per-song lookup bag. Like braccato's ProviderContext plus a memo so original
/// and timed providers can share one LRCLIB catalog fetch without mixing layers.
/// Memo keys include recovered native names so a romaji miss can retry.
/// </summary>
public sealed class ProviderContext
{
    private readonly Dictionary<string, object?> _memo = new(StringComparer.Ordinal);

    public ProviderContext(TrackQuery query, CancellationToken cancellationToken)
    {
        Query = query;
        CancellationToken = cancellationToken;
    }

    public TrackQuery Query { get; set; }

    public CancellationToken CancellationToken { get; }

    public string QueryIdentity =>
        Query.CacheKey + "\0" + (Query.RecoveredTitle ?? string.Empty) + "\0" + (Query.RecoveredArtist ?? string.Empty);

    public async Task<T?> MemoAsync<T>(string key, Func<Task<T?>> factory)
        where T : class
    {
        var full = key + "\0" + QueryIdentity;
        if (_memo.TryGetValue(full, out var boxed))
        {
            return boxed as T;
        }

        var created = await factory().ConfigureAwait(false);
        _memo[full] = created;
        return created;
    }
}
