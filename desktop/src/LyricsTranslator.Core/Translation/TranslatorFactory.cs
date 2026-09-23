using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Settings;

namespace LyricsTranslator.Core.Translation;

public sealed class TranslatorFactory
{
    private readonly ClaudeLyricsTranslator _claude;
    private readonly OpenAiLyricsTranslator _openAi;
    private readonly GeminiLyricsTranslator _gemini;
    private readonly Func<AppSettings> _settings;

    public TranslatorFactory(HttpClient http, Func<AppSettings> settings)
    {
        _settings = settings;
        _claude = new ClaudeLyricsTranslator(http, settings);
        _openAi = new OpenAiLyricsTranslator(http, settings);
        _gemini = new GeminiLyricsTranslator(http, settings);
    }

    public ILyricsTranslator Create() =>
        _settings().AiProvider switch
        {
            AiProvider.OpenAI => _openAi,
            AiProvider.Gemini => _gemini,
            _ => _claude,
        };
}
