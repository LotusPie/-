using System.Text.Json.Serialization;
using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Settings;

public sealed class AppSettings
{
    public AiProvider AiProvider { get; set; } = AiProvider.Claude;

    /// <summary>DPAPI-protected API key. Never log or embed this.</summary>
    public string ProtectedApiKey { get; set; } = string.Empty;

    /// <summary>Optional override. Empty means the provider default.</summary>
    public string Model { get; set; } = string.Empty;

    public PlayerPin PlayerPin { get; set; } = PlayerPin.Auto;

    public bool DetectionPaused { get; set; }

    /// <summary>Always-on-top karaoke overlay. Missing JSON (old settings) is treated as on.</summary>
    public bool OverlayEnabled { get; set; } = true;

    [JsonIgnore]
    public string? ApiKey { get; set; }

    public const string DefaultClaudeModel = "claude-sonnet-4-5";
    public const string DefaultOpenAiModel = "gpt-4o";

    /// <summary>Free-tier friendly default for Google AI Studio keys.</summary>
    public const string DefaultGeminiModel = "gemini-2.5-flash";

    public string EffectiveModel =>
        !string.IsNullOrWhiteSpace(Model)
            ? Model.Trim()
            : AiProvider switch
            {
                AiProvider.OpenAI => DefaultOpenAiModel,
                AiProvider.Gemini => DefaultGeminiModel,
                _ => DefaultClaudeModel,
            };
}
