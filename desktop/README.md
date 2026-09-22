# 歌詞翻譯（Windows 桌面 v1）

聽 **Apple Music**（Microsoft Store）或瀏覽器裡的 **YouTube Music** 時，系統匣小視窗顯示歌名、原文、台灣繁體譯詞，並標示來源（社群／LRCLIB、AI、手貼）。

這是**個人本機工具**，不是 Microsoft Store 應用。不要把歌詞庫散佈出去。

## 系統需求

- Windows 10 版本 1809（10.0.17763）或更新，或 Windows 11
- 建置：Visual Studio 2022（含 .NET 桌面開發 + Windows App SDK）或 .NET 8 SDK + Windows App SDK 工作負載
- 執行：x64 建議；可另編 x86 / ARM64
- **無法在 Linux 上編譯或執行 WinUI 專案。** 這個 repo 的雲端環境若是 Linux，只能跑 `LyricsTranslator.Core` 單元測試。

## 功能（第一刀）

已接上：

- 系統匣 + 小視窗（原文／繁中並排、來源標籤）
- SMTC 列舉工作階段，過濾 Apple Music 與瀏覽器；可用設定釘選
- LRCLIB 查原文 + SQLite 本機快取
- 有原文、沒有人工繁中時，用你自己的 Claude 或 OpenAI 金鑰翻譯
- 找不到原文時**不發明歌詞**，只讓你貼上原文再譯
- 設定：API 金鑰（DPAPI 保護）、模型名稱、播放來源釘選

刻意不做／仍是 stub：

- 卡拉 OK／逐行同步（v1 不做）
- Musixmatch 或其他授權歌詞 API
- th-ch / YTMDesktop / port 9863（瀏覽器 SMTC 為主，不要求 Companion）
- 上架 Microsoft Store、單檔 exe 安裝程式（發佈是 unpackaged 資料夾）
- macOS、Spotify、KKBOX
- 封面縮圖、手動搜尋歌名（可貼原文；改歌請切播放器或釘選來源）

## 怎麼建

在 **Windows** 上：

```bat
cd desktop
dotnet restore LyricsTranslator.sln
dotnet test tests\LyricsTranslator.Core.Tests\LyricsTranslator.Core.Tests.csproj
dotnet build src\LyricsTranslator.App\LyricsTranslator.App.csproj -c Release -p:Platform=x64
```

發佈 unpackaged 自含執行檔（資料夾，不是單一 exe）：

```bat
dotnet publish src\LyricsTranslator.App\LyricsTranslator.App.csproj -c Release -p:Platform=x64 -p:PublishProfile=win-x64
```

輸出在 `src\LyricsTranslator.App\bin\publish\win-x64\`。執行 `LyricsTranslator.exe`。

Visual Studio：開啟 `desktop\LyricsTranslator.sln`，將 `LyricsTranslator.App` 設為起始專案，偵錯設定選 **LyricsTranslator (Unpackaged)**。

## API 金鑰放哪裡

1. 啟動應用 → **設定**
2. 選 Claude 或 OpenAI，貼上你的金鑰
3. 可留空模型名稱（預設 Claude `claude-sonnet-4-5`、OpenAI `gpt-4o`）
4. 儲存

金鑰以目前 Windows 使用者 DPAPI 寫入：

`%LOCALAPPDATA%\LyricsTranslator\settings.json`

**不要**把金鑰寫進程式、repo、環境變數範例或 CI。譯文快取在：

`%LOCALAPPDATA%\LyricsTranslator\lyrics.db`

## 歌詞來源

| 步驟 | 行為 |
| --- | --- |
| 1 | 本機 SQLite 快取 |
| 2 | [LRCLIB](https://lrclib.net/docs) 社群**原文**（標「社群／LRCLIB」） |
| 3 | 若原文已是繁中，直接顯示，不呼叫 AI |
| 4 | 否則用你的金鑰翻成台灣繁體（標「AI」） |
| 5 | LRCLIB 沒有這首歌：請手貼原文（標「手貼」），禁止模型憑歌名瞎寫 |

LRCLIB 要求 `User-Agent`；本應用使用 `LyricsTranslator/1.0`。

## 播放來源

- **Apple Music**：Store App 的 SMTC。會拆 `歌手 — 專輯`，且不盲信 Paused。
- **YouTube Music**：Chrome / Edge / Firefox 等瀏覽器的 Media Session。SMTC **沒有網址**，無法保證一定是 `music.youtube.com`。請在設定釘選「只跟瀏覽器」或「只跟 Apple Music」。
- 列舉 `GetSessions()`，不盲信 `GetCurrentSession()`。

## 授權姿態

個人電腦上顯示與快取。不上架、不散佈歌詞資料庫、不爬歌詞網站。
