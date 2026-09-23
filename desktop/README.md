# 歌詞翻譯（Windows 桌面 v1）

聽 **Apple Music**（Microsoft Store）或瀏覽器裡的 **YouTube Music** 時，系統匣主視窗顯示歌名、全文原文／繁中；另有一個**永遠在最上層、可拖曳的歌詞浮窗**跟著播放走。

這是**個人本機工具**，不是 Microsoft Store 應用。不要把歌詞庫散佈出去。

**從 git pull 之後必須在 Windows 上重新建置**（`dotnet build` / `dotnet publish` 或 Visual Studio）。只更新原始碼不會自動換成新的 exe。

## 系統需求

- Windows 10 版本 1809（10.0.17763）或更新，或 Windows 11
- 建置：Visual Studio 2022（含 .NET 桌面開發 + Windows App SDK）或 .NET 8 SDK + Windows App SDK 工作負載
- 執行：x64 建議；可另編 x86 / ARM64
- **無法在 Linux 上編譯或執行 WinUI 專案。** 這個 repo 的雲端環境若是 Linux，只能跑 `LyricsTranslator.Core` 單元測試。

## 功能（第一刀）

已接上：

- 系統匣 + 主視窗（全文原文／繁中並排、來源標籤）
- **歌詞浮窗（移動視窗）**：永遠最上層、可拖曳；現在行放大、前後行淡出。**有 LRC 就對時間戳對到畫面上的繁中行**（譯詞行數不同會依時間／索引對齊，不會用整首歌等分）。沒有 LRC 不會用進度比例亂跳。設定可調 **同步偏移（秒）**（預設 0）
- SMTC 列舉工作階段，過濾 Apple Music 與瀏覽器；可用設定釘選。進度用 Position + PlaybackRate + LastUpdated 每 100ms 內插，Apple Music 的假 Paused／缺時間軸會另外處理。
- SMTC 列舉工作階段，過濾 Apple Music 與瀏覽器；可用設定釘選
- 日文歌先查巴哈姆特創作大廳的社群繁中譯詞（本機快取，不內建歌詞庫）
- LRCLIB 查原文／LRC + SQLite 本機快取
- 有原文、沒有人工繁中時，用你自己的 Claude、OpenAI 或 **Gemini** 金鑰，依**整曲意境**翻譯
- 找不到原文時**不發明歌詞**，只讓你貼上原文再譯
- 設定：API 金鑰（DPAPI 保護）、模型名稱、播放來源釘選

刻意不做／仍是 stub：

- 像素級逐字卡拉 OK（沒有 LRC 時不假裝有）
- Musixmatch 或其他授權歌詞 API
- th-ch / YTMDesktop / port 9863（瀏覽器 SMTC 為主，不要求 Companion）
- 上架 Microsoft Store、單檔 exe 安裝程式（發佈是 unpackaged 資料夾）
- macOS、Spotify、KKBOX
- 封面縮圖、手動搜尋歌名（可貼原文；改歌請切播放器或釘選來源）

## 怎麼建

在 **Windows** 上，先 `git pull`，再於 **`desktop` 資料夾**重編（不要在 repo 根目錄建；csproj 在 `desktop\` 底下）。不必另外安裝 Visual Studio 的 Windows 10 SDK 22621：C# 目標套件從 NuGet 來。

PowerShell（以本機路徑為例）：

```powershell
Set-Location D:\tool\lyrics-translator\desktop
git pull
dotnet restore .\LyricsTranslator.sln
dotnet test .\tests\LyricsTranslator.Core.Tests\LyricsTranslator.Core.Tests.csproj
dotnet build .\src\LyricsTranslator.App\LyricsTranslator.App.csproj -c Release -p:Platform=x64
```

發佈 unpackaged 自含執行檔（資料夾，不是單一 exe）：

```powershell
Set-Location D:\tool\lyrics-translator\desktop
dotnet publish .\src\LyricsTranslator.App\LyricsTranslator.App.csproj -c Release -p:Platform=x64 -p:PublishProfile=win-x64
```

輸出在 `src\LyricsTranslator.App\bin\publish\win-x64\`。執行 `LyricsTranslator.exe`。

有歌詞時會自動跳出**歌詞浮窗**（預設開）。抓上方「移動歌詞 · 拖曳這裡」可拖到螢幕任意位置。主視窗或系統匣選「顯示／隱藏歌詞浮窗」；浮窗 ✕ 只是隱藏，不會結束程式。關掉主視窗進系統匣時，浮窗仍會留在最上層。

Visual Studio：開啟 `desktop\LyricsTranslator.sln`，將 `LyricsTranslator.App` 設為起始專案，偵錯設定選 **LyricsTranslator (Unpackaged)**。

## API 金鑰放哪裡

1. 啟動應用 → **設定**
2. 供應商選 **Claude**、**OpenAI** 或 **Gemini**
3. 貼上你的金鑰（Gemini 用 [Google AI Studio](https://aistudio.google.com/apikey) 的免費 API key）
4. 可留空模型名稱。預設：
   - Claude：`claude-sonnet-4-5`
   - OpenAI：`gpt-4o`
   - Gemini：`gemini-2.5-flash`（2026 年免費額度友善的 Flash；若碰到額度再改成 `gemini-2.0-flash` 或 `gemini-2.5-flash-lite`）
5. 儲存

金鑰以目前 Windows 使用者 DPAPI 寫入：

`%LOCALAPPDATA%\LyricsTranslator\settings.json`

**不要**把金鑰寫進程式、repo、環境變數範例或 CI。譯文快取在：

`%LOCALAPPDATA%\LyricsTranslator\lyrics.db`

## 歌詞來源

| 步驟 | 行為 |
| --- | --- |
| 1 | 本機 SQLite 快取（含 LRC 時間軸） |
| 2 | 日文歌：查 [巴哈姆特創作大廳](https://home.gamer.com.tw/) 社群繁中譯詞。命中則顯示並標「巴哈姆特」。找不到或逾時就往下走，**不發明** |
| 3 | [LRCLIB](https://lrclib.net/docs) 社群**原文**與 **synced LRC**（標「社群／LRCLIB」） |
| 4 | 若原文已是繁中，直接顯示，不呼叫 AI |
| 5 | 否則把完整原文 + 歌名／歌手／專輯交給你的金鑰，依整曲意境翻成台灣繁體（標「AI」） |
| 6 | 沒有原文：請手貼（標「手貼」），禁止模型憑歌名瞎寫 |

LRCLIB 要求 `User-Agent`；本應用使用 `LyricsTranslator/1.0`。巴哈姆特只在本機抓 HTML、本機快取，不打包歌詞。

## 播放來源

- **Apple Music**：Store App 的 SMTC。會拆 `歌手 — 專輯`，且不盲信 Paused。
- **YouTube Music**：Chrome / Edge / Firefox 等瀏覽器的 Media Session。SMTC **沒有網址**，無法保證一定是 `music.youtube.com`。請在設定釘選「只跟瀏覽器」或「只跟 Apple Music」。
- 列舉 `GetSessions()`，不盲信 `GetCurrentSession()`。
- 歌詞視窗用 SMTC `Position` + `PlaybackRate` + `LastUpdatedTime` 每 ~100ms 內插成播放頭；有 LRC 就把時間戳對到**畫面上的繁中行**（行數不同時依索引把 LRC 時間拉開／收攏）。沒有 LRC **不會**用整首歌等分時長。SMTC 比聲音慢時，到設定調「同步偏移（秒）」，預設 0。

## 授權姿態

個人電腦上顯示與快取。不上架、不散佈歌詞資料庫。巴哈姆特是此個人工具指定的社群譯詞來源，不是給商店用的爬蟲。
