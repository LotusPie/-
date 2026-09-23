using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.NowPlaying;

public interface INowPlayingSource : IAsyncDisposable
{
    event EventHandler<NowPlayingSession?>? SessionChanged;
    event EventHandler<PlaybackProgress>? ProgressChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
}
