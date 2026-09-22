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

    public SmtcNowPlayingSource(SettingsStore settings)
    {
        _settings = settings;
        _settings.Changed += (_, _) => _ = RefreshAsync();
    }

    public event EventHandler<NowPlayingSession?>? SessionChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += (_, _) => _ = RefreshAsync();
        _manager.CurrentSessionChanged += (_, _) => _ = RefreshAsync();
        await RefreshAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_manager is not null)
        {
            _manager.SessionsChanged -= (_, _) => { };
            _manager.CurrentSessionChanged -= (_, _) => { };
        }

        UnhookAll();
        await Task.CompletedTask;
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
        SessionChanged?.Invoke(this, picked);
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
                session.TimelinePropertiesChanged += OnSessionEvent;
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
            session.TimelinePropertiesChanged -= OnSessionEvent;
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

        TimeSpan? duration = null;
        var isPlaying = false;
        try
        {
            var timeline = session.GetTimelineProperties();
            if (timeline.EndTime > TimeSpan.Zero)
            {
                duration = timeline.EndTime;
            }

            var playback = session.GetPlaybackInfo();
            var status = playback.PlaybackStatus;
            isPlaying = status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if (kind == PlayerKind.AppleMusic &&
                !string.IsNullOrWhiteSpace(props.Title) &&
                status is not GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed
                    and not GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped)
            {
                // Apple Music often reports Paused while audio is actually playing.
                isPlaying = true;
            }
        }
        catch
        {
            // Keep metadata even if playback/timeline fails.
        }

        return new NowPlayingSession(
            SourceAppId: sourceId,
            Title: props.Title,
            Artist: props.Artist,
            Album: string.IsNullOrWhiteSpace(props.AlbumTitle) ? null : props.AlbumTitle,
            Duration: duration,
            IsPlaying: isPlaying,
            PlayerKind: kind);
    }
}
