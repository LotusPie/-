using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LyricsTranslator.Core.Settings;

namespace LyricsTranslator.Core.Translation;

public sealed class OpenAiLyricsTranslator : ILyricsTranslator
{
    private readonly HttpClient _http;
    private readonly Func<AppSettings> _settings;

    public OpenAiLyricsTranslator(HttpClient http, Func<AppSettings> settings)
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

        var payload = new
        {
            model = settings.EffectiveModel,
            temperature = 0.4,
            messages = new[]
            {
                new { role = "system", content = TranslationPrompt.SystemPrompt },
                new { role = "user", content = TranslationPrompt.BuildUserPrompt(request) },
            },
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ClaudeLyricsTranslator.DescribeHttpError(response.StatusCode, "OpenAI"));
        }

        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("OpenAI 沒有回傳譯文。");
        }

        return text.TrimEnd();
    }
}
