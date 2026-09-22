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
    private readonly Func<ILyricsTranslator> _translator;
    private readonly Func<AppSettings> _settings;

    public LyricsPipeline(
        ILyricsCache cache,
        ILrclibClient lrclib,
        IBahamutClient bahamut,
        Func<ILyricsTranslator> translator,
        Func<AppSettings> settings)
    {
        _cache = cache;
        _lrclib = lrclib;
        _bahamut = bahamut;
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
        if (cached is { OriginalLyrics: { Length: > 0 }, Translation: { Length: > 0 } } &&
            !IsStaleChineseMisdetect(cached))
        {
            var synced = cached.SyncedLyrics;
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
        string? translation = cached is not null && !IsStaleChineseMisdetect(cached) ? cached.Translation : null;
        var translationSource = string.IsNullOrWhiteSpace(translation) ? LyricsSource.None : cached!.TranslationSource;

        if (string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(syncedLyrics))
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
                // Fall through; Bahamut / paste / AI may still work.
            }

            if (hit is not null)
            {
                lrclibId = hit.Id;
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
            }
        }

        if (instrumental && string.IsNullOrWhiteSpace(translation))
        {
            var record = BuildRecord(query, null, LyricsSource.Lrclib, null, LyricsSource.None, lrclibId, syncedLyrics);
            await _cache.UpsertAsync(record, cancellationToken).ConfigureAwait(false);
            return ToDisplay(query, null, null, LyricsSource.Lrclib, LyricsSource.None, LyricsStatus.Instrumental, "這首歌是純音樂，沒有歌詞。", syncedLyrics);
        }

        if (string.IsNullOrWhiteSpace(translation) && BahamutParser.ShouldSearch(query))
        {
            try
            {
                var community = await _bahamut.FindAsync(query, cancellationToken).ConfigureAwait(false);
                if (community is not null && !string.IsNullOrWhiteSpace(community.Translation))
                {
                    translation = community.Translation;
                    translationSource = LyricsSource.Bahamut;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Timeout / HTML change → AI or paste.
            }
        }

        if (string.IsNullOrWhiteSpace(original) && string.IsNullOrWhiteSpace(translation))
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

        if (string.IsNullOrWhiteSpace(original) && !string.IsNullOrWhiteSpace(translation))
        {
            var communityOnly = BuildRecord(query, null, LyricsSource.None, translation, translationSource, lrclibId, syncedLyrics);
            await _cache.UpsertAsync(communityOnly, cancellationToken).ConfigureAwait(false);
            return ToDisplay(query, null, translation, LyricsSource.None, translationSource, LyricsStatus.Ready, "已找到社群繁中譯詞；原文可再手貼。", syncedLyrics);
        }

        if (LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(original) &&
            !LanguageDetector.LooksLikeSimplifiedChinese(original) &&
            string.IsNullOrWhiteSpace(translation))
        {
            var ready = BuildRecord(query, original, originalSource, original, originalSource, lrclibId, syncedLyrics);
            await _cache.UpsertAsync(ready, cancellationToken).ConfigureAwait(false);
            return ToDisplay(query, original, original, originalSource, originalSource, LyricsStatus.Ready, "原文已是繁體中文。", syncedLyrics);
        }

        if (!string.IsNullOrWhiteSpace(translation))
        {
            var stored = BuildRecord(query, original, originalSource, translation, translationSource, lrclibId, syncedLyrics);
            await _cache.UpsertAsync(stored, cancellationToken).ConfigureAwait(false);
            return ToDisplay(query, original, translation, originalSource, translationSource, LyricsStatus.Ready, null, syncedLyrics);
        }

        return await TranslateAndStoreAsync(query, original!, originalSource, lrclibId, previous: null, hint: null, syncedLyrics, cancellationToken)
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
        return await TranslateAndStoreAsync(query, original, LyricsSource.Paste, cached?.LrclibId, previous: null, hint: null, cached?.SyncedLyrics, cancellationToken)
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

        return await TranslateAndStoreAsync(
                query,
                cached.OriginalLyrics,
                cached.OriginalSource,
                cached.LrclibId,
                cached.Translation,
                hint,
                cached.SyncedLyrics,
                cancellationToken)
            .ConfigureAwait(false);
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

    private static bool IsStaleChineseMisdetect(CachedLyrics cached) =>
        string.Equals(cached.OriginalLyrics, cached.Translation, StringComparison.Ordinal) &&
        !LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(cached.OriginalLyrics);
}
