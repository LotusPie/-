using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Providers;

public sealed class LyricProviderResult
{
    public required string ProviderKey { get; init; }

    public required LyricsSource Source { get; init; }

    /// <summary>Translation, original plain text, or raw LRC depending on <see cref="LyricLayer"/>.</summary>
    public string? Text { get; init; }

    public string? SourceTitle { get; init; }

    public string? SourceHref { get; init; }

    public string? SiteLabel { get; init; }

    public string? TrackName { get; init; }

    public string? ArtistName { get; init; }

    public long? LrclibId { get; init; }

    public bool Instrumental { get; init; }

    public bool HasContent => !string.IsNullOrWhiteSpace(Text);
}
