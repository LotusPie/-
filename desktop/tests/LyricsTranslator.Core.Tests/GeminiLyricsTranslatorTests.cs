using System.Net;
using System.Text;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;
using LyricsTranslator.Core.Settings;
using LyricsTranslator.Core.Translation;

namespace LyricsTranslator.Core.Tests;

public class GeminiLyricsTranslatorTests
{
    [Fact]
    public async Task Posts_to_google_with_user_key_header_and_reads_text()
    {
        var handler = new StubHandler
        {
            ResponseJson =
                """
                {"candidates":[{"content":{"parts":[{"text":"在夜裡奔馳"}]}}]}
                """,
        };
        using var http = new HttpClient(handler);
        var translator = new GeminiLyricsTranslator(http, () => new AppSettings
        {
            AiProvider = AiProvider.Gemini,
            ApiKey = "test-gemini-key",
        });
        var query = TrackNormalizer.FromRaw("夜に駆ける", "YOASOBI", null, null, "chrome", PlayerKind.Browser, true);

        var text = await translator.TranslateAsync(
            new TranslationRequest(query, "夜に駆ける", null, null),
            CancellationToken.None);

        Assert.Equal("在夜裡奔馳", text);
        Assert.Contains("generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent", handler.LastUrl, StringComparison.Ordinal);
        Assert.False(handler.LastUrl!.Contains("test-gemini-key", StringComparison.Ordinal));
        Assert.Equal("test-gemini-key", handler.LastApiKeyHeader);
        Assert.Contains("systemInstruction", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("YOASOBI", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("through-line", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"temperature\":0.7", handler.LastBody, StringComparison.Ordinal);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public string ResponseJson { get; init; } = "{}";
        public string? LastUrl { get; private set; }
        public string? LastApiKeyHeader { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri?.ToString();
            LastApiKeyHeader = request.Headers.TryGetValues("x-goog-api-key", out var values)
                ? values.FirstOrDefault()
                : null;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
