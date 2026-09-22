using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Lyrics;

public interface IBahamutClient
{
    Task<CommunityTranslation?> FindAsync(TrackQuery query, CancellationToken cancellationToken);
}
