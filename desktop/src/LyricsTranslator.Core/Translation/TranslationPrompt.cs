namespace LyricsTranslator.Core.Translation;

public static class TranslationPrompt
{
    public const string SystemPrompt =
        """
        你是把「整首歌」當成一首作品來譯的歌詞譯者，不是逐詞字典，也不是解說員。

        先默讀使用者提供的完整原文（與歌名／歌手／專輯一起當語境），抓住這首歌的意境、語氣、情緒弧線（甜／喪／諷刺／熱血／克制／決絕等 through-line），再用台灣繁體中文歌詞體寫出。譯文讀起來要像同一首歌在唱歌，不要像一行一行查字典。

        硬性規則：
        - 只翻譯使用者提供的完整原文，禁止憑歌名或記憶補寫、發明、擴寫不存在的歌詞。
        - 盡量一行對一行，空行保留；不要寫成散文。行數盡量與原文相同。
        - 允許為了語氣而換詞、調語序，但不要合併成說明句，也不要把隱喻講白或加括號註解。
        - 用台灣華語歌詞用詞。禁止無故簡轉繁、禁止 OpenCC 腔、禁止逐詞英譯腔。
        - 專有名詞、人名、品牌可留原文。日／韓歌常見不譯的副歌或感嘆可保留。
        - 不要擅自加「啊／喔」湊字數。
        - 只輸出譯文本身，不要加「譯文：」標題，不要重複原文。
        """;

    public static string BuildUserPrompt(TranslationRequest request)
    {
        var q = request.Query;
        var meta = $"歌名：{q.DisplayTitle}\n歌手：{q.DisplayArtist}";
        if (!string.IsNullOrWhiteSpace(q.Album))
        {
            meta += $"\n專輯：{q.Album}";
        }

        if (q.Duration is { } duration)
        {
            meta += $"\n時長：{(int)duration.TotalSeconds} 秒";
        }

        var body =
            $"""
            {meta}

            請把上面的歌名／歌手／專輯與下面的完整原文當成同一首歌。先抓住整曲意境，再落成歌詞體譯文。

            完整原文：
            ---
            {request.OriginalLyrics.TrimEnd()}
            ---

            風格提醒（不要抄襲例句）：
            - 英文 R&B：口語、黏、留白，不要書面。
            - 日語城市流行／J-pop：涼或決絕都要有畫面，不要解釋景物。
            - K-pop：節奏短句、副歌可以更口號，專有名詞可留英文。
            """;

        if (!string.IsNullOrWhiteSpace(request.PreviousTranslation))
        {
            body +=
                $"""


                上一版譯文不夠對味，太像逐行查字典，沒抓住整曲意境。請先重抓語氣與 through-line 再寫，不要把隱喻講白。
                """;
            if (!string.IsNullOrWhiteSpace(request.UserHint))
            {
                body += $"\n使用者指示：{request.UserHint.Trim()}";
            }

            body +=
                $"""


                上一版譯文：
                ---
                {request.PreviousTranslation.TrimEnd()}
                ---
                仍須盡量一行對一行，只輸出新的譯文。
                """;
        }

        return body;
    }
}
