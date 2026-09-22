namespace LyricsTranslator.Core.Models;

public sealed class CachedLyrics
{
    public required string CacheKey { get; init; }
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public string? Album { get; init; }
    public int? DurationSeconds { get; init; }
    public string? OriginalLyrics { get; init; }
    public LyricsSource OriginalSource { get; init; }
    public string? Translation { get; init; }
    public LyricsSource TranslationSource { get; init; }
    public long? LrclibId { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
