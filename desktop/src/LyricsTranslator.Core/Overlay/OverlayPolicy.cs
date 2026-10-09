using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Sync;

namespace LyricsTranslator.Core.Overlay;

/// <summary>
/// Karaoke overlay is on by default whenever a track has displayable lines
/// (原文, 繁中, or timed LRC). User can still hide it from the main button or tray.
/// The overlay consumes a <see cref="SyncedLineStream"/> (playhead + composed lines),
/// not a mixed fetch result.
/// </summary>
public static class OverlayPolicy
{
    public static bool DefaultEnabled => true;

    public static bool ShouldShow(bool overlayEnabled, bool hasLyricLines) =>
        overlayEnabled && hasLyricLines;

    public static IReadOnlyList<TimedLyric> LinesForOverlay(
        LyricsStatus status,
        string? original,
        string? translation,
        string? syncedLrc)
    {
        if (status is LyricsStatus.Idle or LyricsStatus.Loading or LyricsStatus.Instrumental)
        {
            return [];
        }

        return LyricTrack.Build(original, translation, syncedLrc);
    }

    public static SyncedLineStream CreateStream(
        LyricsStatus status,
        string? original,
        string? translation,
        string? syncedLrc)
    {
        var stream = new SyncedLineStream();
        stream.Reset(LinesForOverlay(status, original, translation, syncedLrc));
        return stream;
    }

    public static bool HasLyricLines(IReadOnlyList<TimedLyric> lines) => lines.Count > 0;
}
