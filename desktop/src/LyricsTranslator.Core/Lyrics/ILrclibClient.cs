using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Lyrics;

public interface ILrclibClient
{
    Task<LrclibTrack?> FindAsync(TrackQuery query, CancellationToken cancellationToken);
}
