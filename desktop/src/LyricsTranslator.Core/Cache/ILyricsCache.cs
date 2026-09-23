using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Cache;

public interface ILyricsCache
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<CachedLyrics?> GetAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task UpsertAsync(CachedLyrics record, CancellationToken cancellationToken = default);
}
