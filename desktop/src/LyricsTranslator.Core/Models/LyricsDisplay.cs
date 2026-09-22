namespace LyricsTranslator.Core.Models;

public sealed record LyricsDisplay(
    string Title,
    string Artist,
    string? Album,
    string? OriginalLyrics,
    string? Translation,
    LyricsSource OriginalSource,
    LyricsSource TranslationSource,
    string SourceLabel,
    LyricsStatus Status,
    string? Message)
{
    public static LyricsDisplay Idle(string message) => new(
        Title: "未在播放",
        Artist: string.Empty,
        Album: null,
        OriginalLyrics: null,
        Translation: null,
        OriginalSource: LyricsSource.None,
        TranslationSource: LyricsSource.None,
        SourceLabel: string.Empty,
        Status: LyricsStatus.Idle,
        Message: message);

    public static LyricsDisplay Loading(TrackQuery query) => new(
        Title: query.DisplayTitle,
        Artist: query.DisplayArtist,
        Album: query.Album,
        OriginalLyrics: null,
        Translation: null,
        OriginalSource: LyricsSource.None,
        TranslationSource: LyricsSource.None,
        SourceLabel: string.Empty,
        Status: LyricsStatus.Loading,
        Message: "正在查歌詞…");
}
