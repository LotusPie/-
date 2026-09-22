namespace LyricsTranslator.Core.Translation;

public static class TranslationPrompt
{
    public const string SystemPrompt =
        """
        你是歌詞譯者，不是字典，也不是解說員。把完整原文譯成台灣繁體中文歌詞體。

        硬性規則：
        - 只翻譯使用者提供的原文，禁止憑歌名或記憶補寫、發明、擴寫不存在的歌詞。
        - 一行對一行；空行保留。不要寫成散文。行數必須與原文相同。
        - 保留隱喻、重複、語氣（甜／喪／諷刺／熱血）。不要把比喻講白，不要加註解。
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

            以下是完整原文（逐行）：
            ---
            {request.OriginalLyrics.TrimEnd()}
            ---

            參考風格（不要抄襲例句內容）：
            - 英文 R&B：口語、黏、留白，不要書面。
            - 日語城市流行：涼、畫面感，不要解釋景物。
            - K-pop：節奏短句、副歌可以更口號，專有名詞可留英文。
            """;

        if (!string.IsNullOrWhiteSpace(request.PreviousTranslation) &&
            !string.IsNullOrWhiteSpace(request.UserHint))
        {
            body +=
                $"""


                上一版譯文不夠對味。使用者指示：{request.UserHint.Trim()}

                上一版譯文：
                ---
                {request.PreviousTranslation.TrimEnd()}
                ---
                請依指示重譯，仍須一行對一行。
                """;
        }

        return body;
    }
}
