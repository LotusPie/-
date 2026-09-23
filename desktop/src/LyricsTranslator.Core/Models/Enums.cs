namespace LyricsTranslator.Core.Models;

public enum PlayerKind
{
    Unknown,
    AppleMusic,
    Browser,
    Other,
}

public enum PlayerPin
{
    Auto,
    AppleMusic,
    YouTubeMusic,
}

public enum AiProvider
{
    Claude,
    OpenAI,
    Gemini,
}

public enum LyricsSource
{
    None,
    Lrclib,
    Ai,
    Paste,
    Bahamut,
    Web,
}

public enum LyricsStatus
{
    Idle,
    Loading,
    Ready,
    NeedsPaste,
    NeedsApiKey,
    Instrumental,
    Error,
}
