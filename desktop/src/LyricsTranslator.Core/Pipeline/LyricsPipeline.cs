using LyricsTranslator.Core.Cache;
using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Settings;
using LyricsTranslator.Core.Translation;

namespace LyricsTranslator.Core.Pipeline;

public sealed class LyricsPipeline
{
    private readonly ILyricsCache _cache;
    private readonly ILrclibClient _lrclib;
    private readonly IBahamutClient _bahamut;
    private readonly IWebLyricsClient _web;
    private readonly Func<ILyricsTranslator> _translator;
    private readonly Func<AppSettings> _settings;

    public LyricsPipeline(
        ILyricsCache cache,
        ILrclibClient lrclib,
        IBahamutClient bahamut,
        IWebLyricsClient web,
        Func<ILyricsTranslator> translator,
        Func<AppSettings> settings)
    {
        _cache = cache;
        _lrclib = lrclib;
        _bahamut = bahamut;
        _web = web;
        _translator = translator;
        _settings = settings;
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
                synced = await TrySyncedFromLrclibAsync(query, cancellationToken).ConfigureAwait(false);
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

        string? original = cached?.OriginalLyrics;
        var originalSource = cached?.OriginalSource ?? LyricsSource.None;
        long? lrclibId = cached?.LrclibId;
        string? syncedLyrics = cached?.SyncedLyrics;
        var instrumental = false;
        string? aiFallback = cached is { TranslationSource: LyricsSource.Ai } && HasUsableTranslation(cached)
            ? cached.Translation
            : null;

        // Community scrape BEFORE any LLM, even if an old AI row is in SQLite.
        var community = await TryCommunityAsync(query, cancellationToken).ConfigureAwait(false);
        if (community is not null)
        {
            query = query.WithRecovered(
                BahamutParser.ExtractNativeTitle(community.SourceTitle),
                BahamutParser.ExtractNativeArtist(community.SourceTitle));
        }

        string? translation = community?.Translation;
        var translationSource = community is null ? LyricsSource.None : BahamutParser.SourceFromSite(community.SiteLabel);

        // LRCLIB only fills original / LRC. Missing 繁中 is not a reason to call it
        // (that used to skip Bahamut and jump to Gemini). Retry community after
        // LRCLIB recovers native names so romaji SMTC still finds 日+羅+中 posts.
        var needLrclib = string.IsNullOrWhiteSpace(original) ||
                         string.IsNullOrWhiteSpace(syncedLyrics);
        if (needLrclib)
        {
            LrclibTrack? hit = null;
            try
            {
                hit = await _lrclib.FindAsync(query, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Fall through; community / paste / AI may still work.
            }

            if (hit is not null)
            {
                lrclibId = hit.Id;
                query = query.WithRecovered(hit.TrackName, hit.ArtistName);

                if (string.IsNullOrWhiteSpace(syncedLyrics) && !string.IsNullOrWhiteSpace(hit.SyncedLyrics))
                {
                    syncedLyrics = hit.SyncedLyrics;
                }

                if (hit.Instrumental && string.IsNullOrWhiteSpace(hit.EffectivePlainLyrics))
                {
                    instrumental = true;
                }
                else if (string.IsNullOrWhiteSpace(original) && !string.IsNullOrWhiteSpace(hit.EffectivePlainLyrics))
                {
                    original = hit.EffectivePlainLyrics;
                    originalSource = LyricsSource.Lrclib;
                }

                if (string.IsNullOrWhiteSpace(translation))
                {
                    community = await TryCommunityAsync(query, cancellationToken).ConfigureAwait(false);
                    if (community is not null)
                    {
                        translation = community.Translation;
                        translationSource = BahamutParser.SourceFromSite(community.SiteLabel);
                    }
                }
            }
        }

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
                "找不到原文歌詞。不會憑空發明；請貼上原文後再翻譯。",
                syncedLyrics);
        }

        if (LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(original) &&
            !LanguageDetector.LooksLikeSimplifiedChinese(original))
        {
            var ready = BuildRecord(query, original, originalSource, original, originalSource, lrclibId, syncedLyrics);
            await _cache.UpsertAsync(ready, cancellationToken).ConfigureAwait(false);
            return ToDisplay(query, original, original, originalSource, originalSource, LyricsStatus.Ready, "原文已是繁體中文。", syncedLyrics);
        }

        if (!string.IsNullOrWhiteSpace(aiFallback))
        {
            var keepAi = BuildRecord(query, original, originalSource, aiFallback, LyricsSource.Ai, lrclibId, syncedLyrics);
            await _cache.UpsertAsync(keepAi, cancellationToken).ConfigureAwait(false);
            return ToDisplay(query, original, aiFallback, originalSource, LyricsSource.Ai, LyricsStatus.Ready, null, syncedLyrics);
        }

        return await TranslateAndStoreAsync(query, original, originalSource, lrclibId, previous: null, hint: null, syncedLyrics, cancellationToken)
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
        var synced = cached?.SyncedLyrics;
        if (string.IsNullOrWhiteSpace(synced))
        {
            synced = await TrySyncedFromLrclibAsync(query, cancellationToken).ConfigureAwait(false);
        }

        var community = await TryCommunityAsync(query, cancellationToken).ConfigureAwait(false);
        if (community is not null && !string.IsNullOrWhiteSpace(community.Translation))
        {
            var source = BahamutParser.SourceFromSite(community.SiteLabel);
            var stored = BuildRecord(query, original, LyricsSource.Paste, community.Translation, source, cached?.LrclibId, synced);
            await _cache.UpsertAsync(stored, cancellationToken).ConfigureAwait(false);
            return ToDisplay(query, original, community.Translation, LyricsSource.Paste, source, LyricsStatus.Ready, null, synced);
        }

        return await TranslateAndStoreAsync(query, original, LyricsSource.Paste, cached?.LrclibId, previous: null, hint: null, synced, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LyricsDisplay> RetryTranslationAsync(
        TrackQuery query,
        string hint,
        CancellationToken cancellationToken)
    {
        var cached = await _cache.GetAsync(query.CacheKey, cancellationToken).ConfigureAwait(false);
        if (cached is null || string.IsNullOrWhiteSpace(cached.OriginalLyrics))
        {
            return ToDisplay(query, null, null, LyricsSource.None, LyricsSource.None, LyricsStatus.NeedsPaste, "沒有原文，無法重譯。");
        }

        var synced = cached.SyncedLyrics;
        if (string.IsNullOrWhiteSpace(synced))
        {
            synced = await TrySyncedFromLrclibAsync(query, cancellationToken).ConfigureAwait(false);
        }

        return await TranslateAndStoreAsync(
                query,
                cached.OriginalLyrics,
                cached.OriginalSource,
                cached.LrclibId,
                cached.Translation,
                hint,
                synced,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<CommunityTranslation?> TryCommunityAsync(TrackQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var bahamut = await _bahamut.FindAsync(query, cancellationToken).ConfigureAwait(false);
            if (bahamut is not null && !string.IsNullOrWhiteSpace(bahamut.Translation))
            {
                return bahamut;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Timeout / HTML change → web search or AI.
        }

        try
        {
            var web = await _web.FindAsync(query, cancellationToken).ConfigureAwait(false);
            if (web is not null && !string.IsNullOrWhiteSpace(web.Translation))
            {
                return web;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Optional fallback; AI is last resort.
        }

        return null;
    }

    private async Task<string?> TrySyncedFromLrclibAsync(TrackQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var hit = await _lrclib.FindAsync(query, cancellationToken).ConfigureAwait(false);
            return hit?.SyncedLyrics;
        }
        catch
        {
            return null;
        }
    }

    private async Task<LyricsDisplay> TranslateAndStoreAsync(
        TrackQuery query,
        string original,
        LyricsSource originalSource,
        long? lrclibId,
        string? previous,
        string? hint,
        string? syncedLyrics,
        CancellationToken cancellationToken)
    {
        var settings = _settings();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            var pending = BuildRecord(query, original, originalSource, null, LyricsSource.None, lrclibId, syncedLyrics);
            await _cache.UpsertAsync(pending, cancellationToken).ConfigureAwait(false);
            return ToDisplay(
                query,
                original,
                null,
                originalSource,
                LyricsSource.None,
                LyricsStatus.NeedsApiKey,
                "已有原文，但還沒有 API 金鑰。到設定貼上 Claude、OpenAI 或 Gemini 金鑰後再譯。",
                syncedLyrics);
        }

        string translated;
        try
        {
            translated = await _translator()
                .TranslateAsync(new TranslationRequest(query, original, previous, hint), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var failed = BuildRecord(query, original, originalSource, previous, previous is null ? LyricsSource.None : LyricsSource.Ai, lrclibId, syncedLyrics);
            await _cache.UpsertAsync(failed, cancellationToken).ConfigureAwait(false);
            return Error(query, original, originalSource, previous, previous is null ? LyricsSource.None : LyricsSource.Ai, ex.Message, syncedLyrics);
        }

        var stored = BuildRecord(query, original, originalSource, translated, LyricsSource.Ai, lrclibId, syncedLyrics);
        await _cache.UpsertAsync(stored, cancellationToken).ConfigureAwait(false);
        return ToDisplay(query, original, translated, originalSource, LyricsSource.Ai, LyricsStatus.Ready, null, syncedLyrics);
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

    private static LyricsDisplay Error(
        TrackQuery query,
        string? original,
        LyricsSource originalSource,
        string? translation,
        LyricsSource translationSource,
        string message,
        string? syncedLyrics) => ToDisplay(query, original, translation, originalSource, translationSource, LyricsStatus.Error, message, syncedLyrics);

    private static bool IsCommunitySource(LyricsSource source) =>
        source is LyricsSource.Bahamut or LyricsSource.Web;

    private static bool HasUsableTranslation(CachedLyrics cached) =>
        !string.IsNullOrWhiteSpace(cached.Translation) &&
        !IsStaleChineseMisdetect(cached);

    private static bool IsStaleChineseMisdetect(CachedLyrics cached) =>
        string.Equals(cached.OriginalLyrics, cached.Translation, StringComparison.Ordinal) &&
        !LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(cached.OriginalLyrics);
}
