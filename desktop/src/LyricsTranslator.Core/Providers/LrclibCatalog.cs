using LyricsTranslator.Core.Lyrics;

namespace LyricsTranslator.Core.Providers;

/// <summary>
/// Shared LRCLIB Find for original + timed wrappers so tests that stub one
/// <see cref="ILrclibClient"/> still get both plain text and SyncedLyrics.
/// </summary>
public sealed class LrclibCatalog
{
    private readonly ILrclibClient _lrclib;

    public LrclibCatalog(ILrclibClient lrclib)
    {
        _lrclib = lrclib;
    }

    public Task<LrclibTrack?> GetAsync(ProviderContext context) =>
        context.MemoAsync("lrclib-catalog", async () =>
        {
            try
            {
                return await _lrclib.FindAsync(context.Query, context.CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        });
}
