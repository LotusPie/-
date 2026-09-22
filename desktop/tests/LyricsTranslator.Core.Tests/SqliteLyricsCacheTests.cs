using LyricsTranslator.Core.Cache;
using LyricsTranslator.Core.Models;

namespace LyricsTranslator.Core.Tests;

public class SqliteLyricsCacheTests
{
    [Fact]
    public async Task Round_trips_a_record()
    {
        var path = Path.Combine(Path.GetTempPath(), "lyrics-translator-tests", Guid.NewGuid() + ".db");
        try
        {
            var cache = new SqliteLyricsCache(path);
            await cache.InitializeAsync();
            var record = new CachedLyrics
            {
                CacheKey = "adele\thello",
                Title = "Hello",
                Artist = "Adele",
                OriginalLyrics = "Hello from the other side",
                OriginalSource = LyricsSource.Lrclib,
                Translation = "你好",
                TranslationSource = LyricsSource.Ai,
                LrclibId = 42,
                UpdatedAt = DateTimeOffset.Parse("2026-09-22T00:00:00Z"),
            };
            await cache.UpsertAsync(record);
            var loaded = await cache.GetAsync(record.CacheKey);
            Assert.NotNull(loaded);
            Assert.Equal("你好", loaded!.Translation);
            Assert.Equal(LyricsSource.Lrclib, loaded.OriginalSource);
            Assert.Equal(42, loaded.LrclibId);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
