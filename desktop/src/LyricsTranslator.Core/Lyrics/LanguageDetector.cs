using System.Globalization;

namespace LyricsTranslator.Core.Lyrics;

public static class LanguageDetector
{
    // Typical simplified-only forms that almost never appear in Taiwan Traditional copy.
    private static readonly char[] SimplifiedOnly =
    [
        '这', '那', '个', '们', '来', '对', '会', '时', '过', '说', '为', '发', '经', '国', '乐', '爱', '听',
        '见', '让', '从', '应', '当', '还', '没', '么', '为', '里', '后', '开', '关', '门', '问', '间',
        '东', '车', '飞', '风', '头', '体', '发', '觉', '离', '欢', '实', '现', '点', '将', '无', '与',
    ];

    private static readonly char[] TraditionalOnly =
    [
        '這', '個', '們', '來', '對', '會', '時', '過', '說', '為', '發', '經', '國', '樂', '愛', '聽',
        '見', '讓', '從', '應', '當', '還', '沒', '麼', '裡', '後', '開', '關', '門', '問', '間',
        '東', '車', '飛', '風', '頭', '體', '覺', '離', '歡', '實', '現', '點', '將', '無', '與',
    ];

    public static bool HasCjk(string text) => text.Any(IsCjk);

    public static bool LooksLikeTraditionalChinese(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !HasCjk(text))
        {
            return false;
        }

        var trad = Count(text, TraditionalOnly);
        var simp = Count(text, SimplifiedOnly);
        return trad > 0 && trad >= simp * 2;
    }

    public static bool LooksLikeSimplifiedChinese(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !HasCjk(text))
        {
            return false;
        }

        var trad = Count(text, TraditionalOnly);
        var simp = Count(text, SimplifiedOnly);
        return simp > 0 && simp >= trad * 2;
    }

    public static bool LooksLikeAlreadyTaiwanMandarinLyrics(string? text)
        => LooksLikeTraditionalChinese(text) && !LooksLikeSimplifiedChinese(text);

    private static int Count(string text, char[] needles)
    {
        var set = needles.ToHashSet();
        return text.Count(set.Contains);
    }

    private static bool IsCjk(char c)
    {
        var cat = CharUnicodeInfo.GetUnicodeCategory(c);
        return c is >= '\u4E00' and <= '\u9FFF' || cat == UnicodeCategory.OtherLetter && c > 127;
    }
}
