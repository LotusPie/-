using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;
using LyricsTranslator.Core.Settings;
using LyricsTranslator.Core.Translation;

namespace LyricsTranslator.Core.Tests;

public class TranslationPromptTests
{
    [Fact]
    public void User_prompt_includes_full_original_and_forbids_invention_in_system_prompt()
    {
        Assert.Contains("禁止憑歌名或記憶補寫", TranslationPrompt.SystemPrompt);
        var query = TrackNormalizer.FromRaw("Hello", "Adele", "25", TimeSpan.FromSeconds(295), "chrome", PlayerKind.Browser, true);
        var prompt = TranslationPrompt.BuildUserPrompt(new TranslationRequest(query, "Hello from the other side", null, null));
        Assert.Contains("Hello from the other side", prompt);
        Assert.Contains("Adele", prompt);
        Assert.Contains("25", prompt);
        Assert.Contains("專輯", prompt);
        Assert.Contains("完整原文", prompt);
        Assert.Contains("意境", TranslationPrompt.SystemPrompt);
        Assert.Contains("隱喻", TranslationPrompt.SystemPrompt);
    }

    [Fact]
    public void Retry_without_hint_still_asks_to_recapture_mood()
    {
        var query = TrackNormalizer.FromRaw("Hello", "Adele", "25", TimeSpan.FromSeconds(295), "chrome", PlayerKind.Browser, true);
        var prompt = TranslationPrompt.BuildUserPrompt(new TranslationRequest(query, "Hello from the other side", "你好從另一邊", UserHint: null));
        Assert.Contains("不夠對味", prompt);
        Assert.Contains("查字典", prompt);
        Assert.Contains("你好從另一邊", prompt);
    }
}
