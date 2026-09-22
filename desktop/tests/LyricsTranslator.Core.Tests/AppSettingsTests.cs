using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Settings;

namespace LyricsTranslator.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Default_models_match_providers()
    {
        Assert.Equal("claude-sonnet-4-5", new AppSettings { AiProvider = AiProvider.Claude }.EffectiveModel);
        Assert.Equal("gpt-4o", new AppSettings { AiProvider = AiProvider.OpenAI }.EffectiveModel);
        Assert.Equal("gemini-2.5-flash", new AppSettings { AiProvider = AiProvider.Gemini }.EffectiveModel);
        Assert.Equal("gemini-2.0-flash", new AppSettings
        {
            AiProvider = AiProvider.Gemini,
            Model = "gemini-2.0-flash",
        }.EffectiveModel);
    }
}
