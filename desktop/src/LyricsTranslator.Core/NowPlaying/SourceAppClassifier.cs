using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.NowPlaying;

public static class SourceAppClassifier
{
    public static PlayerKind Classify(string? sourceAppUserModelId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppUserModelId))
        {
            return PlayerKind.Unknown;
        }

        var id = sourceAppUserModelId.Trim();

        if (Contains(id, "AppleInc.iTunes") || EndsWithExe(id, "itunes"))
        {
            return PlayerKind.Other;
        }

        if (Contains(id, "AppleInc.AppleMusicWin") ||
            Contains(id, "AppleMusicWin") ||
            (Contains(id, "AppleMusic") && !Contains(id, "iTunes")))
        {
            return PlayerKind.AppleMusic;
        }

        if (IsBrowser(id))
        {
            return PlayerKind.Browser;
        }

        return PlayerKind.Other;
    }

    public static bool IsBrowser(string sourceAppUserModelId)
    {
        var id = sourceAppUserModelId;
        return Contains(id, "chrome")
               || Contains(id, "msedge")
               || Contains(id, "microsoftedge")
               || Contains(id, "firefox")
               || Contains(id, "brave")
               || Contains(id, "opera")
               || Contains(id, "vivaldi")
               || Contains(id, "chromium")
               || Contains(id, "arc");
    }

    private static bool Contains(string id, string token) =>
        id.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static bool EndsWithExe(string id, string name) =>
        id.EndsWith(name, StringComparison.OrdinalIgnoreCase) ||
        id.EndsWith(name + ".exe", StringComparison.OrdinalIgnoreCase);
}
