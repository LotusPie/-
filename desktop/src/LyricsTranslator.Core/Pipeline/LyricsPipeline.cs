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
    private readonly Func<ILyricsTranslator> _translator;
    private readonly Func<AppSettings> _settings;

    public LyricsPipeline(
        ILyricsCache cache,
        ILrclibClient lrclib,
        Func<ILyricsTranslator> translator,
        Func<AppSettings> settings)
    {
        _cache = cache;
        _lrclib = lrclib;
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
            return ToDisplay(query, cached.OriginalLyrics, cached.Translation, cached.OriginalSource, cached.TranslationSource, LyricsStatus.Ready, null);
        }

        string? original = cached?.OriginalLyrics;
        var originalSource = cached?.OriginalSource ?? LyricsSource.None;
        long? lrclibId = cached?.LrclibId;
        var instrumental = false;

        if (string.IsNullOrWhiteSpace(original))
        {
            LrclibTrack? hit;
            try
            {
                hit = await _lrclib.FindAsync(query, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Error(query, original, originalSource, null, LyricsSource.None, $"查 LRCLIB 失敗：{ex.Message}");
            }

            if (hit is not null)
            {
                lrclibId = hit.Id;
                if (hit.Instrumental && string.IsNullOrWhiteSpace(hit.EffectivePlainLyrics))
                {
                    instrumental = true;
                }
                else
                {
                    original = hit.EffectivePlainLyrics;
                    originalSource = LyricsSource.Lrclib;
                }
            }
        }

        if (instrumental)
        {
            var record = BuildRecord(query, null, LyricsSource.Lrclib, null, LyricsSource.None, lrclibId);
            await _cache.UpsertAsync(record, cancellationToken).ConfigureAwait(false);
            return ToDisplay(query, null, null, LyricsSource.Lrclib, LyricsSource.None, LyricsStatus.Instrumental, "這首歌是純音樂，沒有歌詞。");
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
                "找不到原文歌詞。不會憑空發明；請貼上原文後再翻譯。");
        }

        if (LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(original) &&
            !LanguageDetector.LooksLikeSimplifiedChinese(original))
        {
            var ready = BuildRecord(query, original, originalSource, original, originalSource, lrclibId);
            await _cache.UpsertAsync(ready, cancellationToken).ConfigureAwait(false);
            return ToDisplay(query, original, original, originalSource, originalSource, LyricsStatus.Ready, "原文已是繁體中文。");
        }

        if (!string.IsNullOrWhiteSpace(cached?.Translation) &&
            !IsStaleChineseMisdetect(cached))
        {
            return ToDisplay(query, original, cached.Translation, originalSource, cached.TranslationSource, LyricsStatus.Ready, null);
        }

        return await TranslateAndStoreAsync(query, original, originalSource, lrclibId, previous: null, hint: null, cancellationToken)
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

        return await TranslateAndStoreAsync(query, original, LyricsSource.Paste, lrclibId: null, previous: null, hint: null, cancellationToken)
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
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<LyricsDisplay> TranslateAndStoreAsync(
        TrackQuery query,
        string original,
        LyricsSource originalSource,
        long? lrclibId,
        string? previous,
        string? hint,
        CancellationToken cancellationToken)
    {
        var settings = _settings();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            var pending = BuildRecord(query, original, originalSource, null, LyricsSource.None, lrclibId);
            await _cache.UpsertAsync(pending, cancellationToken).ConfigureAwait(false);
            return ToDisplay(
                query,
                original,
                null,
                originalSource,
                LyricsSource.None,
                LyricsStatus.NeedsApiKey,
                "已有原文，但還沒有 API 金鑰。到設定貼上 Claude、OpenAI 或 Gemini 金鑰後再譯。");
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
            var failed = BuildRecord(query, original, originalSource, previous, previous is null ? LyricsSource.None : LyricsSource.Ai, lrclibId);
            await _cache.UpsertAsync(failed, cancellationToken).ConfigureAwait(false);
            return Error(query, original, originalSource, previous, previous is null ? LyricsSource.None : LyricsSource.Ai, ex.Message);
        }

        var stored = BuildRecord(query, original, originalSource, translated, LyricsSource.Ai, lrclibId);
        await _cache.UpsertAsync(stored, cancellationToken).ConfigureAwait(false);
        return ToDisplay(query, original, translated, originalSource, LyricsSource.Ai, LyricsStatus.Ready, null);
    }

    private static CachedLyrics BuildRecord(
        TrackQuery query,
        string? original,
        LyricsSource originalSource,
        string? translation,
        LyricsSource translationSource,
        long? lrclibId) => new()
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
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static LyricsDisplay ToDisplay(
        TrackQuery query,
        string? original,
        string? translation,
        LyricsSource originalSource,
        LyricsSource translationSource,
        LyricsStatus status,
        string? message) => new(
        Title: query.DisplayTitle,
        Artist: query.DisplayArtist,
        Album: query.Album,
        OriginalLyrics: original,
        Translation: translation,
        OriginalSource: originalSource,
        TranslationSource: translationSource,
        SourceLabel: SourceLabelFormatter.Format(originalSource, translationSource, status),
        Status: status,
        Message: message);

    private static LyricsDisplay Error(
        TrackQuery query,
        string? original,
        LyricsSource originalSource,
        string? translation,
        LyricsSource translationSource,
        string message) => ToDisplay(query, original, translation, originalSource, translationSource, LyricsStatus.Error, message);

    /// <summary>
    /// Older builds cached Japanese originals as their own "translation" after a 繁中 false positive.
    /// Identical original/translation is only a finished result when the text really is 繁中.
    /// </summary>
    private static bool IsStaleChineseMisdetect(CachedLyrics cached) =>
        string.Equals(cached.OriginalLyrics, cached.Translation, StringComparison.Ordinal) &&
        !LanguageDetector.LooksLikeAlreadyTaiwanMandarinLyrics(cached.OriginalLyrics);
}
