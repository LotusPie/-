using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.NowPlaying;
using LyricsTranslator.Core.Settings;
using Windows.Media.Control;

namespace LyricsTranslator.NowPlaying;

public sealed class SmtcNowPlayingSource : INowPlayingSource
{
    private readonly SettingsStore _settings;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> _hooked = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private int _refreshSerial;
    private Timer? _progressTimer;
    private GlobalSystemMediaTransportControlsSession? _timelineSession;

    public SmtcNowPlayingSource(SettingsStore settings)
    {
        _settings = settings;
        _settings.Changed += (_, _) => _ = RefreshAsync();
    }

    public event EventHandler<NowPlayingSession?>? SessionChanged;
    public event EventHandler<PlaybackProgress>? ProgressChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += (_, _) => _ = RefreshAsync();
        _manager.CurrentSessionChanged += (_, _) => _ = RefreshAsync();
        _progressTimer = new Timer(_ => PollProgress(), null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        await RefreshAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_progressTimer is not null)
        {
            await _progressTimer.DisposeAsync().ConfigureAwait(false);
            _progressTimer = null;
        }

        if (_manager is not null)
        {
            _manager.SessionsChanged -= (_, _) => { };
            _manager.CurrentSessionChanged -= (_, _) => { };
        }

        UnhookAll();
        Volatile.Write(ref _timelineSession, null);
    }

    private async Task RefreshAsync()
    {
        var serial = Interlocked.Increment(ref _refreshSerial);
        var manager = _manager;
        if (manager is null)
        {
            return;
        }

        List<NowPlayingSession> mapped = [];
        try
        {
            var sessions = manager.GetSessions();
            Hook(sessions);
            foreach (var session in sessions)
            {
                var item = await MapAsync(session).ConfigureAwait(false);
                if (item is not null)
                {
                    mapped.Add(item);
                }
            }
        }
        catch
        {
            mapped = [];
        }

        if (serial != Volatile.Read(ref _refreshSerial))
        {
            return;
        }

        var snapshot = _settings.Snapshot();
        var picked = SessionSelector.Pick(mapped, snapshot.PlayerPin, snapshot.DetectionPaused);
        lock (_gate)
        {
            if (picked is null)
            {
                Volatile.Write(ref _timelineSession, null);
            }
            else if (_hooked.TryGetValue(picked.SourceAppId, out var smtc))
            {
                Volatile.Write(ref _timelineSession, smtc);
            }
        }

        SessionChanged?.Invoke(this, picked);
        if (picked is not null)
        {
            ProgressChanged?.Invoke(this, ToProgress(picked));
        }
    }

    private void PollProgress()
    {
        var session = Volatile.Read(ref _timelineSession);
        if (session is null)
        {
            return;
        }

        try
        {
            ProgressChanged?.Invoke(this, ReadProgress(session));
        }
        catch
        {
            // Session may already be gone; next RefreshAsync will clear it.
        }
    }

    private void Hook(IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions)
    {
        lock (_gate)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var session in sessions)
            {
                string id;
                try
                {
                    id = session.SourceAppUserModelId ?? Guid.NewGuid().ToString();
                }
                catch
                {
                    continue;
                }

                seen.Add(id);
                if (_hooked.ContainsKey(id))
                {
                    continue;
                }

                session.MediaPropertiesChanged += OnSessionEvent;
                session.PlaybackInfoChanged += OnSessionEvent;
                _hooked[id] = session;
            }

            foreach (var stale in _hooked.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                if (_hooked.Remove(stale, out var session))
                {
                    Unhook(session);
                }
            }
        }
    }

    private void UnhookAll()
    {
        lock (_gate)
        {
            foreach (var session in _hooked.Values)
            {
                Unhook(session);
            }

            _hooked.Clear();
        }
    }

    private void Unhook(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            session.MediaPropertiesChanged -= OnSessionEvent;
            session.PlaybackInfoChanged -= OnSessionEvent;
        }
        catch
        {
            // Session may already be gone.
        }
    }

    private void OnSessionEvent(GlobalSystemMediaTransportControlsSession sender, object args) =>
        _ = RefreshAsync();

    private static async Task<NowPlayingSession?> MapAsync(GlobalSystemMediaTransportControlsSession session)
    {
        string sourceId;
        try
        {
            sourceId = session.SourceAppUserModelId ?? string.Empty;
        }
        catch
        {
            return null;
        }

        var kind = SourceAppClassifier.Classify(sourceId);
        GlobalSystemMediaTransportControlsSessionMediaProperties? props;
        try
        {
            props = await session.TryGetMediaPropertiesAsync();
        }
        catch
        {
            return null;
        }

        if (props is null)
        {
            return null;
        }

        var progress = ReadProgress(session, kind);
        return new NowPlayingSession(
            SourceAppId: sourceId,
            Title: props.Title,
            Artist: props.Artist,
            Album: string.IsNullOrWhiteSpace(props.AlbumTitle) ? null : props.AlbumTitle,
            Duration: progress.Duration,
            Position: progress.Position,
            IsPlaying: progress.IsPlaying,
            PlayerKind: kind,
            PlaybackRate: progress.PlaybackRate,
            TimelineLastUpdated: progress.LastUpdated);
    }

    private static PlaybackProgress ReadProgress(GlobalSystemMediaTransportControlsSession session)
    {
        PlayerKind kind;
        try
        {
            kind = SourceAppClassifier.Classify(session.SourceAppUserModelId);
        }
        catch
        {
            kind = PlayerKind.Unknown;
        }

        return ReadProgress(session, kind);
    }

    private static PlaybackProgress ReadProgress(
        GlobalSystemMediaTransportControlsSession session,
        PlayerKind kind)
    {
        TimeSpan position = TimeSpan.Zero;
        TimeSpan? duration = null;
        var isPlaying = false;
        var rate = 1.0;
        DateTimeOffset? lastUpdated = null;

        try
        {
            var timeline = session.GetTimelineProperties();
            if (timeline.EndTime > TimeSpan.Zero)
            {
                duration = timeline.EndTime;
            }

            if (timeline.Position >= TimeSpan.Zero)
            {
                position = timeline.Position;
            }

            lastUpdated = ToTimestamp(timeline.LastUpdatedTime);
        }
        catch
        {
            // Apple Music Store App sometimes has no timeline; keep metadata + playing flag.
        }

        try
        {
            var playback = session.GetPlaybackInfo();
            isPlaying = IsEffectivelyPlaying(kind, playback.PlaybackStatus);
            if (playback.PlaybackRate is double playbackRate && playbackRate > 0)
            {
                rate = playbackRate;
            }
        }
        catch
        {
            if (kind == PlayerKind.AppleMusic)
            {
                isPlaying = true;
            }
        }

        return new PlaybackProgress(position, duration, isPlaying, rate, lastUpdated);
    }

    private static PlaybackProgress ToProgress(NowPlayingSession session) =>
        new(session.Position, session.Duration, session.IsPlaying, session.PlaybackRate, session.TimelineLastUpdated);

    internal static bool IsEffectivelyPlaying(
        PlayerKind kind,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus status)
    {
        if (status is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed
            or GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped)
        {
            return false;
        }

        if (kind == PlayerKind.AppleMusic)
        {
            // Apple Music often reports Paused while audio is actually playing.
            return true;
        }

        return status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
    }

    private static DateTimeOffset? ToTimestamp(DateTimeOffset value)
    {
        if (value == default || value.Year < 2000)
        {
            return null;
        }

        return value;
    }
}
