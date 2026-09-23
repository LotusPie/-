using LyricsTranslator.Core.Lyrics;

namespace LyricsTranslator.Core.Tests;

internal static class BahamutFixture
{
    public const string SunnyArtworkUrl = "https://home.gamer.com.tw/artwork.php?sn=5859521";
    public const string SunnySn = "5859521";

    public static string PathToSunnyArtwork =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "bahamut-5859521.html");

    public static string ReadSunnyArtwork() => File.ReadAllText(PathToSunnyArtwork);

    /// <summary>
    /// Prefer the live artwork page; fall back to the committed fixture when the network is flaky.
    /// </summary>
    public static async Task<string> LoadSunnyArtworkAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(BahamutClient.UserAgent);
            http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-TW,zh;q=0.9");
            var html = await http.GetStringAsync(SunnyArtworkUrl);
            if (html.Contains("你就像微風一般", StringComparison.Ordinal) &&
                html.Contains("article_content", StringComparison.OrdinalIgnoreCase))
            {
                return html;
            }
        }
        catch (Exception)
        {
            // CI / offline: use the fixture checked into the repo.
        }

        return ReadSunnyArtwork();
    }
}
