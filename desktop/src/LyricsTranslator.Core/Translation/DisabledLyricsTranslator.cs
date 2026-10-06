namespace LyricsTranslator.Core.Translation;

/// <summary>
/// AI translation is switched off. The pipeline must never call this.
/// </summary>
public sealed class DisabledLyricsTranslator : ILyricsTranslator
{
    public Task<string> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("AI 翻譯已關閉。只查巴哈姆特／Mojim／LRCLIB，不會呼叫 Gemini／Claude／OpenAI。");
}
