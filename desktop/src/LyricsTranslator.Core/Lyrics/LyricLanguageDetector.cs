using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;

namespace LyricsTranslator.Core.Lyrics;

/// <summary>
/// Language of the playing track from original script (kana/kanji/hangul), not romaji SMTC.
/// Sunny/Aoi Shiori resolve through aliases to 晴る/青い栞.
/// </summary>
public static class LyricLanguageDetector
{
    public static LyricLanguage Detect(TrackQuery query)
    {
        var titles = TrackLookup.Titles(query);
        var artists = TrackLookup.Artists(query);
        foreach (var title in titles)
        {
            if (LanguageDetector.LooksLikeKorean(title))
            {
                return LyricLanguage.Korean;
            }

            if (LanguageDetector.LooksLikeJapanese(title) ||
                LanguageDetector.LooksLikeJapaneseOrKanjiTitle(title))
            {
                return LyricLanguage.Japanese;
            }
        }

        foreach (var artist in artists)
        {
            if (LanguageDetector.LooksLikeKorean(artist))
            {
                return LyricLanguage.Korean;
            }

            if (LanguageDetector.LooksLikeJapanese(artist))
            {
                return LyricLanguage.Japanese;
            }
        }

        var primary = titles.FirstOrDefault() ?? query.DisplayTitle;
        if (LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(primary))
        {
            return LyricLanguage.Other;
        }

        if (IsMostlyLatin(primary))
        {
            return LyricLanguage.English;
        }

        return LyricLanguage.Other;
    }

    private static bool IsMostlyLatin(string value)
    {
        var letters = value.Where(char.IsLetter).ToList();
        if (letters.Count == 0)
        {
            return false;
        }

        return letters.Count(static c => c <= 127) >= letters.Count * 0.8;
    }
}
