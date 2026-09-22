using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.NowPlaying;

public interface INowPlayingSource : IAsyncDisposable
{
    event EventHandler<NowPlayingSession?>? SessionChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
}
