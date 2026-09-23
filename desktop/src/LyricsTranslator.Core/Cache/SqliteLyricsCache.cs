using LyricsTranslator.Core.Models;
using Microsoft.Data.Sqlite;

namespace LyricsTranslator.Core.Cache;

public sealed class SqliteLyricsCache : ILyricsCache, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SqliteLyricsCache(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                CREATE TABLE IF NOT EXISTS lyrics_cache (
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
                  synced_lyrics TEXT,
                  updated_at TEXT NOT NULL
                );
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            cmd.CommandText = "ALTER TABLE lyrics_cache ADD COLUMN synced_lyrics TEXT;";
            try
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException)
            {
                // Column already exists on databases created with the current schema.
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CachedLyrics?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT cache_key, title, artist, album, duration_seconds,
                       original_lyrics, original_source, translation, translation_source,
                       lrclib_id, synced_lyrics, updated_at
                FROM lyrics_cache
                WHERE cache_key = $key
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$key", cacheKey);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return new CachedLyrics
            {
                CacheKey = reader.GetString(0),
                Title = reader.GetString(1),
                Artist = reader.GetString(2),
                Album = reader.IsDBNull(3) ? null : reader.GetString(3),
                DurationSeconds = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                OriginalLyrics = reader.IsDBNull(5) ? null : reader.GetString(5),
                OriginalSource = ParseSource(reader.GetString(6)),
                Translation = reader.IsDBNull(7) ? null : reader.GetString(7),
                TranslationSource = reader.IsDBNull(8) ? LyricsSource.None : ParseSource(reader.GetString(8)),
                LrclibId = reader.IsDBNull(9) ? null : reader.GetInt64(9),
                SyncedLyrics = reader.IsDBNull(10) ? null : reader.GetString(10),
                UpdatedAt = DateTimeOffset.Parse(reader.GetString(11)),
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpsertAsync(CachedLyrics record, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO lyrics_cache (
                  cache_key, title, artist, album, duration_seconds,
                  original_lyrics, original_source, translation, translation_source,
                  lrclib_id, synced_lyrics, updated_at)
                VALUES (
                  $cache_key, $title, $artist, $album, $duration_seconds,
                  $original_lyrics, $original_source, $translation, $translation_source,
                  $lrclib_id, $synced_lyrics, $updated_at)
                ON CONFLICT(cache_key) DO UPDATE SET
                  title = excluded.title,
                  artist = excluded.artist,
                  album = excluded.album,
                  duration_seconds = excluded.duration_seconds,
                  original_lyrics = excluded.original_lyrics,
                  original_source = excluded.original_source,
                  translation = excluded.translation,
                  translation_source = excluded.translation_source,
                  lrclib_id = excluded.lrclib_id,
                  synced_lyrics = excluded.synced_lyrics,
                  updated_at = excluded.updated_at;
                """;
            cmd.Parameters.AddWithValue("$cache_key", record.CacheKey);
            cmd.Parameters.AddWithValue("$title", record.Title);
            cmd.Parameters.AddWithValue("$artist", record.Artist);
            cmd.Parameters.AddWithValue("$album", (object?)record.Album ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$duration_seconds", (object?)record.DurationSeconds ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$original_lyrics", (object?)record.OriginalLyrics ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$original_source", record.OriginalSource.ToString());
            cmd.Parameters.AddWithValue("$translation", (object?)record.Translation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$translation_source", record.TranslationSource.ToString());
            cmd.Parameters.AddWithValue("$lrclib_id", (object?)record.LrclibId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$synced_lyrics", (object?)record.SyncedLyrics ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$updated_at", record.UpdatedAt.ToString("O"));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }

    private static LyricsSource ParseSource(string value) =>
        Enum.TryParse<LyricsSource>(value, out var source) ? source : LyricsSource.None;
}
