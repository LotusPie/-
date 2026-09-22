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

    [JsonIgnore]
    public string? ApiKey { get; set; }

    public string EffectiveModel =>
        !string.IsNullOrWhiteSpace(Model)
            ? Model.Trim()
            : AiProvider == AiProvider.OpenAI
                ? "gpt-4o"
                : "claude-sonnet-4-5";
}
