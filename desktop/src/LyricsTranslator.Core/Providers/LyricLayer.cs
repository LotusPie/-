namespace LyricsTranslator.Core.Providers;

/// <summary>
/// Adapted from better-lyrics/braccato: timed original and translation are
/// separate provider layers, not one first-win dump mixed into the same chain.
/// </summary>
public enum LyricLayer
{
    Translation,
    Original,
    Timed,
}

public enum ProviderSyncType
{
    Line,
    Unsynced,
}
