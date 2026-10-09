using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Lyrics;

public interface IWebLyricsClient
{
    Task<CommunityTranslation?> FindAsync(TrackQuery query, CancellationToken cancellationToken);
}
