namespace LyricsTranslator.Core.Providers;

/// <summary>
/// One lyrics source. Mirrors braccato <c>ProviderRegistration</c>
/// (key, displayName, syncType, fetch) without copying their providers.
/// </summary>
public interface ILyricProvider
{
    string Key { get; }

    string DisplayName { get; }

    LyricLayer Layer { get; }

    ProviderSyncType SyncType { get; }

    Task<LyricProviderResult?> FetchAsync(ProviderContext context);
}
