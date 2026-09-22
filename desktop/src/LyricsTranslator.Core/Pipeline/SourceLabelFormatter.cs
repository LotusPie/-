using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Pipeline;

public static class SourceLabelFormatter
{
    public static string Format(LyricsSource original, LyricsSource translation, LyricsStatus status)
    {
        if (status == LyricsStatus.Instrumental)
        {
            return "純音樂";
        }

        var originalText = original switch
        {
            LyricsSource.Lrclib => "社群／LRCLIB",
            LyricsSource.Paste => "手貼",
            LyricsSource.Ai => "AI",
            _ => null,
        };
        var translationText = translation switch
        {
            LyricsSource.Ai => "AI",
            LyricsSource.Paste => "手貼",
            LyricsSource.Lrclib => "社群／LRCLIB",
            _ => null,
        };

        if (originalText is null && translationText is null)
        {
            return string.Empty;
        }

        if (translationText is null || translationText == originalText)
        {
            return originalText ?? translationText ?? string.Empty;
        }

        if (originalText is null)
        {
            return translationText;
        }

        return $"{originalText} → {translationText}";
    }
}
