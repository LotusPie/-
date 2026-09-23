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
                SyncedLyrics = "[00:01.00] Hello from the other side",
                UpdatedAt = DateTimeOffset.Parse("2026-09-22T00:00:00Z"),
            };
            await cache.UpsertAsync(record);
            var loaded = await cache.GetAsync(record.CacheKey);
            Assert.NotNull(loaded);
            Assert.Equal("你好", loaded!.Translation);
            Assert.Equal(LyricsSource.Lrclib, loaded.OriginalSource);
            Assert.Equal(42, loaded.LrclibId);
            Assert.Equal("[00:01.00] Hello from the other side", loaded.SyncedLyrics);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task Migrates_old_schema_without_synced_lyrics_column()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lyrics-translator-tests");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Guid.NewGuid() + ".db");
        try
        {
            await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}"))
            {
                await connection.OpenAsync();
                await using var cmd = connection.CreateCommand();
                cmd.CommandText =
                    """
                    CREATE TABLE lyrics_cache (
                      cache_key TEXT PRIMARY KEY,
                      title TEXT NOT NULL,
                      artist TEXT NOT NULL,
                      album TEXT,
                      duration_seconds INTEGER,
                      original_lyrics TEXT,
                      original_source TEXT NOT NULL,
                      translation TEXT,
                      translation_source TEXT,
                      lrclib_id INTEGER,
                      updated_at TEXT NOT NULL
                    );
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            var cache = new SqliteLyricsCache(path);
            await cache.InitializeAsync();
            await cache.UpsertAsync(new CachedLyrics
            {
                CacheKey = "yoasobi\t夜に駆ける",
                Title = "夜に駆ける",
                Artist = "YOASOBI",
                OriginalLyrics = "夜に駆ける",
                OriginalSource = LyricsSource.Lrclib,
                Translation = "在夜裡往前衝",
                TranslationSource = LyricsSource.Bahamut,
                SyncedLyrics = "[00:10.00] 夜に駆ける",
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            var loaded = await cache.GetAsync("yoasobi\t夜に駆ける");
            Assert.Equal(LyricsSource.Bahamut, loaded!.TranslationSource);
            Assert.Equal("[00:10.00] 夜に駆ける", loaded.SyncedLyrics);
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
