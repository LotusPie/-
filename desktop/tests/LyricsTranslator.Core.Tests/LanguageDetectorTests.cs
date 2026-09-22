using LyricsTranslator.Core.Lyrics;

namespace LyricsTranslator.Core.Tests;

public class LanguageDetectorTests
{
    private const string TraditionalMandarin =
        """
        這是繁體歌詞
        我不離開
        你的眼睛為什麼還不說
        """;

    private const string TeresaTengStyle =
        """
        月亮代表我的心
        你問我愛你有多深
        """;

    private const string JapaneseKanjiAndKana =
        """
        夜に駆ける
        君の瞳に恋をして
        愛してる
        時を超えて
        """;

    private const string JapaneseHiragana =
        """
        あなたがいるなら
        わたしはわすれない
        """;

    private const string JapaneseKatakana =
        """
        スーパーシャイ
        ベイビー 笑って
        """;

    [Fact]
    public void Traditional_mandarin_lyrics_are_already_zh_hant()
    {
        Assert.True(LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(TraditionalMandarin));
        Assert.True(LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(TeresaTengStyle));
        Assert.False(LanguageDetector.LooksLikeJapanese(TraditionalMandarin));
    }

    [Fact]
    public void Japanese_with_kana_is_not_traditional_chinese()
    {
        Assert.True(LanguageDetector.LooksLikeJapanese(JapaneseKanjiAndKana));
        Assert.False(LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(JapaneseKanjiAndKana));
        Assert.False(LanguageDetector.LooksLikeTraditionalChinese(JapaneseKanjiAndKana));
    }

    [Fact]
    public void Japanese_hiragana_or_katakana_is_not_zh_hant()
    {
        Assert.True(LanguageDetector.LooksLikeJapanese(JapaneseHiragana));
        Assert.True(LanguageDetector.LooksLikeJapanese(JapaneseKatakana));
        Assert.False(LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(JapaneseHiragana));
        Assert.False(LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(JapaneseKatakana));
    }

    [Fact]
    public void Shared_kanji_without_chinese_grammar_is_not_chinese()
    {
        const string kanjiOnly = "愛\n時\n夢\n夜\n心";
        Assert.False(LanguageDetector.LooksLikeJapanese(kanjiOnly));
        Assert.False(LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(kanjiOnly));
        Assert.True(LanguageDetector.HasHan(kanjiOnly));
    }

    [Fact]
    public void Korean_hangul_is_not_zh_hant()
    {
        const string korean = "사랑해 너의 눈빛";
        Assert.True(LanguageDetector.LooksLikeKorean(korean));
        Assert.False(LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(korean));
    }

    [Fact]
    public void Simplified_chinese_is_not_a_finished_taiwan_original()
    {
        const string simplified = "这是简体歌词\n我不离开你的眼睛";
        Assert.False(LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(simplified));
    }
}
