using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LyricsTranslator.Core.Settings;

namespace LyricsTranslator.Core.Translation;

public sealed class GeminiLyricsTranslator : ILyricsTranslator
{
    private readonly HttpClient _http;
    private readonly Func<AppSettings> _settings;

    public GeminiLyricsTranslator(HttpClient http, Func<AppSettings> settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<string> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        var settings = _settings();
        var key = settings.ApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("尚未設定 API 金鑰。");
        }

        var model = Uri.EscapeDataString(settings.EffectiveModel);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        var payload = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = TranslationPrompt.SystemPrompt } },
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = TranslationPrompt.BuildUserPrompt(request) } },
                },
            },
            generationConfig = new
            {
                temperature = 0.7,
                maxOutputTokens = 8192,
            },
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.TryAddWithoutValidation("x-goog-api-key", key);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ClaudeLyricsTranslator.DescribeHttpError(response.StatusCode, "Gemini"));
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("promptFeedback", out var feedback) &&
            feedback.TryGetProperty("blockReason", out var blocked) &&
            blocked.GetString() is { Length: > 0 } reason)
        {
            throw new InvalidOperationException($"Gemini 拒絕了這次翻譯（{reason}）。");
        }

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Gemini 沒有回傳譯文。");
        }

        var text = ReadCandidateText(candidates[0]);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini 沒有回傳譯文。");
        }

        return text.TrimEnd();
    }

    private static string? ReadCandidateText(JsonElement candidate)
    {
        if (!candidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts))
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text))
            {
                builder.Append(text.GetString());
            }
        }

        return builder.ToString();
    }
}
