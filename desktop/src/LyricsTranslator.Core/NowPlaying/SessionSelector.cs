using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.NowPlaying;

public static class SessionSelector
{
    public static NowPlayingSession? Pick(
        IReadOnlyList<NowPlayingSession> sessions,
        PlayerPin pin,
        bool detectionPaused)
    {
        if (detectionPaused || sessions.Count == 0)
        {
            return null;
        }

        var eligible = sessions.Where(s => MatchesPin(s, pin) && s.HasTrackMetadata).ToList();
        if (pin == PlayerPin.YouTubeMusic)
        {
            eligible = eligible.Where(s => !LooksLikeNonMusic(s) || s.IsPlaying).ToList();
        }
        else
        {
            eligible = eligible.Where(s => s.PlayerKind != PlayerKind.Browser || !LooksLikeNonMusic(s)).ToList();
        }

        if (eligible.Count == 0)
        {
            return null;
        }

        return eligible
            .OrderByDescending(s => s.IsPlaying)
            .ThenBy(s => s.PlayerKind == PlayerKind.AppleMusic ? 0 : 1)
            .First();
    }

    public static bool MatchesPin(NowPlayingSession session, PlayerPin pin) => pin switch
    {
        PlayerPin.AppleMusic => session.PlayerKind == PlayerKind.AppleMusic,
        PlayerPin.YouTubeMusic => session.PlayerKind == PlayerKind.Browser,
        _ => session.PlayerKind is PlayerKind.AppleMusic or PlayerKind.Browser,
    };

    public static bool LooksLikeNonMusic(NowPlayingSession session)
    {
        var title = session.Title ?? string.Empty;
        if (title.Contains("podcast", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("episode", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("audiobook", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (session.Duration is { TotalMinutes: > 25 })
        {
            return true;
        }

        return false;
    }
}
