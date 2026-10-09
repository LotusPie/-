using LyricsTranslator.Core.Cache;
using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Providers;
using LyricsTranslator.Core.Settings;
using LyricsTranslator.Core.Translation;

namespace LyricsTranslator.Core.Pipeline;

public sealed class LyricsPipeline
{
    private readonly ILyricsCache _cache;
    private readonly LyricsLookup _lookup;

    public LyricsPipeline(
        ILyricsCache cache,
        ILrclibClient lrclib,
        IBahamutClient bahamut,
        IWebLyricsClient web,
        Func<ILyricsTranslator> translator,
        Func<AppSettings> settings)
        : this(cache, LyricsLookup.FromClients(lrclib, bahamut, web), translator, settings)
    {
    }

    public LyricsPipeline(
        ILyricsCache cache,
        LyricsLookup lookup,
        Func<ILyricsTranslator> translator,
        Func<AppSettings> settings)
    {
        _cache = cache;
        _lookup = lookup;
        _ = translator;
        _ = settings;
    }

    public async Task<LyricsDisplay> ResolveAsync(TrackQuery query, CancellationToken cancellationToken)
    {
        if (!query.HasIdentity)
        {
            return LyricsDisplay.Idle("未偵測到可用的歌名。");
        }

        var cached = await _cache.GetAsync(query.CacheKey, cancellationToken).ConfigureAwait(false);
        var communityCached = cached is not null &&
                              HasUsableTranslation(cached) &&
                              IsCommunitySource(cached.TranslationSource);

        if (communityCached)
        {
            var synced = cached!.SyncedLyrics;
            if (string.IsNullOrWhiteSpace(synced))
            {
                var ctx = new ProviderContext(query, cancellationToken);
                synced = (await _lookup.Timed.FetchAsync(ctx).ConfigureAwait(false))?.Text;
                if (!string.IsNullOrWhiteSpace(synced))
                {
                    await _cache.UpsertAsync(
                            BuildRecord(query, cached.OriginalLyrics, cached.OriginalSource, cached.Translation, cached.TranslationSource, cached.LrclibId, synced),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            return ToDisplay(query, cached.OriginalLyrics, cached.Translation, cached.OriginalSource, cached.TranslationSource, LyricsStatus.Ready, null, synced);
        }

        var context = new ProviderContext(query, cancellationToken);
        string? original = cached?.OriginalLyrics;
        var originalSource = cached?.OriginalSource ?? LyricsSource.None;
        long? lrclibId = cached?.LrclibId;
        string? syncedLyrics = cached?.SyncedLyrics;
        var instrumental = false;

        var translationHit = await _lookup.Translation.FetchAsync(context).ConfigureAwait(false);
        ApplyRecovered(context, translationHit);

        string? translation = translationHit?.Text;
        var translationSource = translationHit?.Source ?? LyricsSource.None;

        var needOriginal = string.IsNullOrWhiteSpace(original);
        var needSynced = string.IsNullOrWhiteSpace(syncedLyrics);
        if (needOriginal || needSynced)
        {
            if (needOriginal)
            {
                var originalHit = await _lookup.Original.FetchAsync(context).ConfigureAwait(false);
                if (originalHit is not null)
                {
                    lrclibId = originalHit.LrclibId ?? lrclibId;
                    ApplyRecovered(context, originalHit);
                    if (originalHit.Instrumental && string.IsNullOrWhiteSpace(originalHit.Text))
                    {
                        instrumental = true;
                    }
                    else if (!string.IsNullOrWhiteSpace(originalHit.Text))
                    {
                        original = originalHit.Text;
                        originalSource = originalHit.Source;
                    }
                }
            }

            if (needSynced)
            {
                var timedHit = await _lookup.Timed.FetchAsync(context).ConfigureAwait(false);
                if (timedHit is not null)
                {
                    lrclibId = timedHit.LrclibId ?? lrclibId;
                    ApplyRecovered(context, timedHit);
                    if (!string.IsNullOrWhiteSpace(timedHit.Text))
                    {
                        syncedLyrics = timedHit.Text;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(translation))
            {
                translationHit = await _lookup.Translation.FetchAsync(context).ConfigureAwait(false);
                ApplyRecovered(context, translationHit);
                translation = translationHit?.Text;
                translationSource = translationHit?.Source ?? LyricsSource.None;
            }
        }

        query = context.Query;

        if (instrumental && string.IsNullOrWhiteSpace(translation))
        {
            var record = BuildRecord(query, null, LyricsSource.Lrclib, null, LyricsSource.None, lrclibId, syncedLyrics);
            await _cache.UpsertAsync(record, cancellationToken).ConfigureAwait(false);
            return ToDisplay(query, null, null, LyricsSource.Lrclib, LyricsSource.None, LyricsStatus.Instrumental, "這首歌是純音樂，沒有歌詞。", syncedLyrics);
        }

        if (!string.IsNullOrWhiteSpace(translation))
        {
            if (string.IsNullOrWhiteSpace(original))
            {
                var communityOnly = BuildRecord(query, null, LyricsSource.None, translation, translationSource, lrclibId, syncedLyrics);
                await _cache.UpsertAsync(communityOnly, cancellationToken).ConfigureAwait(false);
                return ToDisplay(query, null, translation, LyricsSource.None, translationSource, LyricsStatus.Ready, "已找到社群繁中譯詞；原文可再手貼。", syncedLyrics);
            }

            var stored = BuildRecord(query, original, originalSource, translation, translationSource, lrclibId, syncedLyrics);
            await _cache.UpsertAsync(stored, cancellationToken).ConfigureAwait(false);
            return ToDisplay(query, original, translation, originalSource, translationSource, LyricsStatus.Ready, null, syncedLyrics);
        }

        if (string.IsNullOrWhiteSpace(original))
        {
            return ToDisplay(
                query,
                null,
                null,
                LyricsSource.None,
                LyricsSource.None,
                LyricsStatus.NeedsPaste,
                "找不到原文歌詞。不會憑空發明，也不會呼叫 AI；請貼上原文。",
                syncedLyrics);
        }

        if (LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(original) &&
            !LanguageDetector.LooksLikeSimplifiedChinese(original))
        {
            var ready = BuildRecord(query, original, originalSource, original, originalSource, lrclibId, syncedLyrics);
            await _cache.UpsertAsync(ready, cancellationToken).ConfigureAwait(false);
            return ToDisplay(query, original, original, originalSource, originalSource, LyricsStatus.Ready, "原文已是繁體中文。", syncedLyrics);
        }

        return await StoreOriginalWithoutTranslationAsync(
                query, original, originalSource, lrclibId, syncedLyrics, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LyricsDisplay> ApplyPastedOriginalAsync(
        TrackQuery query,
        string pastedOriginal,
        CancellationToken cancellationToken)
    {
        var original = pastedOriginal.Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(original))
        {
            return ToDisplay(query, null, null, LyricsSource.None, LyricsSource.None, LyricsStatus.NeedsPaste, "請貼上原文歌詞。");
        }

        var cached = await _cache.GetAsync(query.CacheKey, cancellationToken).ConfigureAwait(false);
        var context = new ProviderContext(query, cancellationToken);
        var synced = cached?.SyncedLyrics;
        if (string.IsNullOrWhiteSpace(synced))
        {
            synced = (await _lookup.Timed.FetchAsync(context).ConfigureAwait(false))?.Text;
        }

        var community = await _lookup.Translation.FetchAsync(context).ConfigureAwait(false);
        if (community is not null && !string.IsNullOrWhiteSpace(community.Text))
        {
            var stored = BuildRecord(query, original, LyricsSource.Paste, community.Text, community.Source, cached?.LrclibId, synced);
            await _cache.UpsertAsync(stored, cancellationToken).ConfigureAwait(false);
            return ToDisplay(query, original, community.Text, LyricsSource.Paste, community.Source, LyricsStatus.Ready, null, synced);
        }

        return await StoreOriginalWithoutTranslationAsync(
                query, original, LyricsSource.Paste, cached?.LrclibId, synced, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LyricsDisplay> RetryTranslationAsync(
        TrackQuery query,
        string hint,
        CancellationToken cancellationToken)
    {
        _ = hint;
        return await ResolveAsync(query, cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyRecovered(ProviderContext context, LyricProviderResult? hit)
    {
        if (hit is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(hit.SourceTitle))
        {
            context.Query = context.Query.WithRecovered(
                BahamutParser.ExtractNativeTitle(hit.SourceTitle),
                BahamutParser.ExtractNativeArtist(hit.SourceTitle));
        }

        context.Query = context.Query.WithRecovered(hit.TrackName, hit.ArtistName);
    }

    private async Task<LyricsDisplay> StoreOriginalWithoutTranslationAsync(
        TrackQuery query,
        string original,
        LyricsSource originalSource,
        long? lrclibId,
        string? syncedLyrics,
        CancellationToken cancellationToken)
    {
        var stored = BuildRecord(query, original, originalSource, null, LyricsSource.None, lrclibId, syncedLyrics);
        await _cache.UpsertAsync(stored, cancellationToken).ConfigureAwait(false);
        return ToDisplay(
            query,
            original,
            null,
            originalSource,
            LyricsSource.None,
            LyricsStatus.Ready,
            "沒有找到社群繁中。不會呼叫 AI，也不會發明譯詞。",
            syncedLyrics);
    }

    private static CachedLyrics BuildRecord(
        TrackQuery query,
        string? original,
        LyricsSource originalSource,
        string? translation,
        LyricsSource translationSource,
        long? lrclibId,
        string? syncedLyrics) => new()
    {
        CacheKey = query.CacheKey,
        Title = query.DisplayTitle,
        Artist = query.DisplayArtist,
        Album = query.Album,
        DurationSeconds = query.Duration is { } d ? (int)Math.Round(d.TotalSeconds) : null,
        OriginalLyrics = original,
        OriginalSource = originalSource,
        Translation = translation,
        TranslationSource = translationSource,
        LrclibId = lrclibId,
        SyncedLyrics = syncedLyrics,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static LyricsDisplay ToDisplay(
        TrackQuery query,
        string? original,
        string? translation,
        LyricsSource originalSource,
        LyricsSource translationSource,
        LyricsStatus status,
        string? message,
        string? syncedLyrics = null) => new(
        Title: query.DisplayTitle,
        Artist: query.DisplayArtist,
        Album: query.Album,
        OriginalLyrics: original,
        Translation: translation,
        OriginalSource: originalSource,
        TranslationSource: translationSource,
        SourceLabel: SourceLabelFormatter.Format(originalSource, translationSource, status),
        Status: status,
        Message: message,
        SyncedLyrics: syncedLyrics);

    private static bool IsCommunitySource(LyricsSource source) =>
        source is LyricsSource.Bahamut or LyricsSource.Web;

    private static bool HasUsableTranslation(CachedLyrics cached) =>
        !string.IsNullOrWhiteSpace(cached.Translation) &&
        !IsStaleChineseMisdetect(cached);

    private static bool IsStaleChineseMisdetect(CachedLyrics cached) =>
        string.Equals(cached.OriginalLyrics, cached.Translation, StringComparison.Ordinal) &&
        !LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(cached.OriginalLyrics);
}
