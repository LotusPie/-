namespace LyricsTranslator.Core.Lyrics;

public static class LanguageDetector
{
    // Traditional forms that Japanese lyrics almost never use (vs shinjitai / kana).
    // Shared kanji like 愛/時/心/夢 are NOT evidence of Chinese.
    private static readonly char[] DistinctiveTraditional =
    [
        '這', '個', '們', '來', '對', '會', '說', '發', '經', '國', '樂', '聽',
        '讓', '從', '應', '當', '還', '沒', '麼', '裡', '關', '體', '覺', '歡',
        '實', '點', '將', '與', '嗎', '呢', '著', '臺', '灣',
    ];

    private static readonly char[] DistinctiveSimplified =
    [
        '这', '个', '们', '来', '对', '会', '说', '发', '经', '国', '乐', '听',
        '让', '从', '应', '当', '还', '没', '么', '为', '里', '关', '吗', '觉',
        '欢', '实', '点', '将', '与',
    ];

    private static readonly string[] ChineseFunctionWords =
    [
        "什麼", "沒有", "不是", "一個", "因為", "如果", "我們", "你們", "他們",
        "的", "了", "是", "不", "我", "你", "他", "她", "它", "們",
        "這", "那", "嗎", "呢", "吧", "著", "沒", "很", "在", "也",
        "就", "都", "把", "被", "要", "能", "會",
    ];

    public static bool HasHan(string text) => text.Any(IsHan);

    /// <summary>
    /// True for kana and for kanji-only Japanese titles such as 花一匁 (no kana required).
    /// </summary>
    public static bool LooksLikeJapaneseOrKanjiTitle(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || LooksLikeKorean(text))
        {
            return false;
        }

        if (LooksLikeJapanese(text))
        {
            return true;
        }

        return HasHan(text) && !LooksLikeAlreadyTaiwanMandarinLyrics(text);
    }

    public static bool LooksLikeJapanese(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Any(IsKana);
    }

    public static bool LooksLikeKorean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Any(IsHangul);
    }

    public static bool LooksLikeTraditionalChinese(string? text)
    {
        if (!LooksLikeChineseScript(text, out var trad, out var simp))
        {
            return false;
        }

        return trad > 0 && trad >= Math.Max(1, simp) * 2;
    }

    public static bool LooksLikeSimplifiedChinese(string? text)
    {
        if (!LooksLikeChineseScript(text, out var trad, out var simp))
        {
            return false;
        }

        return simp > 0 && simp >= Math.Max(1, trad) * 2;
    }

    /// <summary>
    /// True only for Taiwan/HK Mandarin lyrics that already are 繁中.
    /// Japanese (kana, or kanji+kana) and Han-only text without Chinese grammar must not match.
    /// </summary>
    public static bool LooksLikeAlreadyTaiwanMandarinLyrics(string? text)
    {
        if (!LooksLikeChineseScript(text, out var trad, out var simp))
        {
            return false;
        }

        // Any simplified-leaning text is not a finished 繁中 original.
        return simp == 0 || trad >= simp * 2;
    }

    private static bool LooksLikeChineseScript(string? text, out int distinctiveTraditional, out int distinctiveSimplified)
    {
        distinctiveTraditional = 0;
        distinctiveSimplified = 0;
        if (string.IsNullOrWhiteSpace(text) || !HasHan(text))
        {
            return false;
        }

        if (LooksLikeJapanese(text) || LooksLikeKorean(text))
        {
            return false;
        }

        // Han characters alone are not Chinese. Require Mandarin function words.
        if (CountChineseFunctionWords(text) < 2)
        {
            return false;
        }

        distinctiveTraditional = Count(text, DistinctiveTraditional);
        distinctiveSimplified = Count(text, DistinctiveSimplified);
        return true;
    }

    internal static int CountChineseFunctionWords(string text)
    {
        var count = 0;
        foreach (var word in ChineseFunctionWords)
        {
            count += CountOccurrences(text, word);
        }

        return count;
    }

    private static int CountOccurrences(string text, string needle)
    {
        if (needle.Length == 0)
        {
            return 0;
        }

        var count = 0;
        var start = 0;
        while (start <= text.Length - needle.Length)
        {
            var found = text.IndexOf(needle, start, StringComparison.Ordinal);
            if (found < 0)
            {
                break;
            }

            count++;
            start = found + needle.Length;
        }

        return count;
    }

    private static int Count(string text, char[] needles)
    {
        var set = needles.ToHashSet();
        return text.Count(set.Contains);
    }

    private static bool IsKana(char c) =>
        c is >= '\u3040' and <= '\u309F'     // hiragana
            or >= '\u30A0' and <= '\u30FF'   // katakana (excludes ー which is U+30FC, actually ー is U+30FC in this range)
            or >= '\u31F0' and <= '\u31FF'   // katakana phonetic extensions
            or >= '\uFF66' and <= '\uFF9D';  // halfwidth katakana

    private static bool IsHangul(char c) =>
        c is >= '\uAC00' and <= '\uD7A3'
            or >= '\u1100' and <= '\u11FF'
            or >= '\u3130' and <= '\u318F';

    private static bool IsHan(char c) =>
        c is >= '\u4E00' and <= '\u9FFF'
            or >= '\u3400' and <= '\u4DBF';
}
