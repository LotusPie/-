using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LyricsTranslator.Core.Settings;

namespace LyricsTranslator.Core.Translation;

public sealed class ClaudeLyricsTranslator : ILyricsTranslator
{
    private readonly HttpClient _http;
    private readonly Func<AppSettings> _settings;

    public ClaudeLyricsTranslator(HttpClient http, Func<AppSettings> settings)
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
            max_tokens = 4096,
            system = TranslationPrompt.SystemPrompt,
            messages = new[]
            {
                new { role = "user", content = TranslationPrompt.BuildUserPrompt(request) },
            },
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        httpRequest.Headers.Add("x-api-key", key);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(DescribeHttpError(response.StatusCode, "Claude"));
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("content", out var content) || content.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Claude 沒有回傳譯文。");
        }

        var text = content[0].GetProperty("text").GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Claude 沒有回傳譯文。");
        }

        return text.TrimEnd();
    }

    internal static string DescribeHttpError(System.Net.HttpStatusCode status, string provider) => status switch
    {
        System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
            $"{provider} 金鑰無效或沒有權限。",
        (System.Net.HttpStatusCode)429 =>
            $"{provider} 呼叫過於頻繁，請稍後再試。",
        _ => $"{provider} 翻譯失敗（HTTP {(int)status}）。",
    };
}
