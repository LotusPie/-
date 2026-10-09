using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Translation;

public sealed record TranslationRequest(
    TrackQuery Query,
    string OriginalLyrics,
    string? PreviousTranslation,
    string? UserHint);

public interface ILyricsTranslator
{
    Task<string> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken);
}
