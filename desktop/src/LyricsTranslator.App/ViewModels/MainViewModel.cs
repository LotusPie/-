using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LyricsTranslator.Core.Lyrics;
using LyricsTranslator.Core.Models;
using LyricsTranslator.Core.Normalization;
using LyricsTranslator.Core.NowPlaying;
using LyricsTranslator.Core.Pipeline;
using LyricsTranslator.Core.Settings;
using Microsoft.UI.Dispatching;

namespace LyricsTranslator.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly LyricsPipeline _pipeline;
    private readonly SettingsStore _settings;
    private readonly DispatcherQueue _dispatcher;
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private CancellationTokenSource _cts = new();
    private TrackQuery? _currentQuery;
    private IReadOnlyList<TimedLyric> _track = [];
    private readonly PlaybackInterpolator _clock = new();
    private readonly DispatcherQueueTimer _overlayTimer;
    private TimeSpan _position;
    private TimeSpan? _duration;
    private int _generation;

    public MainViewModel(LyricsPipeline pipeline, SettingsStore settings, DispatcherQueue dispatcher)
    {
        _pipeline = pipeline;
        _settings = settings;
        _dispatcher = dispatcher;
        DetectionPaused = settings.Snapshot().DetectionPaused;
        OverlayEnabled = settings.Snapshot().OverlayEnabled;
        _overlayTimer = dispatcher.CreateTimer();
        _overlayTimer.Interval = TimeSpan.FromMilliseconds(100);
        _overlayTimer.IsRepeating = true;
        _overlayTimer.Tick += (_, _) => TickPlayhead();
        _overlayTimer.Start();
        Apply(LyricsDisplay.Idle("未偵測到 Apple Music 或瀏覽器裡的 YouTube Music。"));
    }

    public ObservableCollection<LyricLineItem> LyricLines { get; } = [];

    public ObservableCollection<LyricLineItem> OverlayLines { get; } = [];

    public event EventHandler<int>? CurrentLineChanged;

    public bool OverlayShouldShow => OverlayEnabled && HasLyricLines;

    [ObservableProperty] private string _title = "未在播放";
    [ObservableProperty] private string _artist = string.Empty;
    [ObservableProperty] private string _album = string.Empty;
    [ObservableProperty] private string _sourceLabel = string.Empty;
    [ObservableProperty] private string _originalLyrics = string.Empty;
    [ObservableProperty] private string _translatedLyrics = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _syncCaption = string.Empty;
    [ObservableProperty] private string _positionLabel = string.Empty;
    [ObservableProperty] private string _pasteText = string.Empty;
    [ObservableProperty] private string _retryHint = string.Empty;
    [ObservableProperty] private bool _needsPaste;
    [ObservableProperty] private bool _needsApiKey;
    [ObservableProperty] private bool _canRetry;
    [ObservableProperty] private bool _detectionPaused;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasLyricLines;
    [ObservableProperty] private bool _overlayEnabled = true;
    [ObservableProperty] private int _currentLineIndex;

    public async Task OnSessionChangedAsync(NowPlayingSession? session)
    {
        if (session is null)
        {
            CancelInFlight();
            _clock.Reset();
            _duration = null;
            Publish(LyricsDisplay.Idle("未偵測到 Apple Music 或瀏覽器裡的 YouTube Music。可在設定釘選播放來源，或暫停偵測。"));
            _currentQuery = null;
            _track = [];
            return;
        }

        var query = TrackNormalizer.FromRaw(
            session.Title,
            session.Artist,
            session.Album,
            session.Duration,
            session.SourceAppId,
            session.PlayerKind,
            session.IsPlaying);

        var sameTrack = _currentQuery is not null && _currentQuery.CacheKey == query.CacheKey;
        if (!sameTrack)
        {
            _clock.Reset();
        }

        OnProgress(new PlaybackProgress(
            session.Position,
            session.Duration,
            session.IsPlaying,
            session.PlaybackRate,
            session.TimelineLastUpdated));

        if (sameTrack && !NeedsPaste && !NeedsApiKey)
        {
            return;
        }

        _currentQuery = query;
        await RunAsync(display => _pipeline.ResolveAsync(query, display), query).ConfigureAwait(false);
    }

    public void OnProgress(PlaybackProgress progress)
    {
        var now = DateTimeOffset.Now;
        var offset = TimeSpan.FromSeconds(
            AppSettings.ClampSyncOffset(_settings.Snapshot().SyncOffsetSeconds));
        var position = PlaybackClock.Playhead(_clock, progress, offset, now);
        _position = position;
        _duration = progress.Duration;
        _dispatcher.TryEnqueue(() => ApplyPlayhead(position, progress.Duration));
    }

    private void TickPlayhead()
    {
        var offset = TimeSpan.FromSeconds(
            AppSettings.ClampSyncOffset(_settings.Snapshot().SyncOffsetSeconds));
        var position = PlaybackClock.PlayheadNow(_clock, offset, _duration, DateTimeOffset.Now);
        _position = position;
        ApplyPlayhead(position, _duration);
    }

    private void ApplyPlayhead(TimeSpan position, TimeSpan? duration)
    {
        PositionLabel = FormatPosition(position, duration);
        HighlightCurrentLine();
    }

    [RelayCommand]
    private async Task PasteOriginalAsync()
    {
        if (_currentQuery is null)
        {
            StatusMessage = "目前沒有正在播放的歌，無法貼上。";
            return;
        }

        await RunAsync(ct => _pipeline.ApplyPastedOriginalAsync(_currentQuery, PasteText, ct), _currentQuery)
            .ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RetryTranslationAsync()
    {
        if (_currentQuery is null)
        {
            return;
        }

        await RunAsync(ct => _pipeline.RetryTranslationAsync(_currentQuery, RetryHint, ct), _currentQuery)
            .ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_currentQuery is null)
        {
            return;
        }

        await RunAsync(ct => _pipeline.ResolveAsync(_currentQuery, ct), _currentQuery).ConfigureAwait(false);
    }

    [RelayCommand]
    private void ToggleOverlay()
    {
        OverlayEnabled = !OverlayEnabled;
    }

    [RelayCommand]
    private void ToggleDetection()
    {
        DetectionPaused = !DetectionPaused;
        _settings.SetDetectionPaused(DetectionPaused);
        if (DetectionPaused)
        {
            CancelInFlight();
            Publish(LyricsDisplay.Idle("已暫停偵測。系統匣再開一次即可繼續。"));
        }
    }

    public async Task ReloadAfterSettingsAsync()
    {
        DetectionPaused = _settings.Snapshot().DetectionPaused;
        if (_currentQuery is null)
        {
            return;
        }

        await RunAsync(ct => _pipeline.ResolveAsync(_currentQuery, ct), _currentQuery).ConfigureAwait(false);
    }

    private async Task RunAsync(Func<CancellationToken, Task<LyricsDisplay>> work, TrackQuery query)
    {
        CancelInFlight();
        var cts = _cts;
        var gen = Interlocked.Increment(ref _generation);
        Publish(LyricsDisplay.Loading(query));
        await _runGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (gen != Volatile.Read(ref _generation))
            {
                return;
            }

            var display = await work(cts.Token).ConfigureAwait(false);
            if (!cts.IsCancellationRequested && gen == Volatile.Read(ref _generation))
            {
                Publish(display);
            }
        }
        catch (OperationCanceledException)
        {
            // Newer track replaced this run.
        }
        catch (Exception ex)
        {
            Publish(new LyricsDisplay(
                query.DisplayTitle,
                query.DisplayArtist,
                query.Album,
                OriginalLyrics,
                TranslatedLyrics,
                LyricsSource.None,
                LyricsSource.None,
                SourceLabel,
                LyricsStatus.Error,
                ex.Message));
        }
        finally
        {
            _runGate.Release();
        }
    }

    private void CancelInFlight()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // ignored
        }

        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    private void Publish(LyricsDisplay display) =>
        _dispatcher.TryEnqueue(() => Apply(display));

    private void Apply(LyricsDisplay display)
    {
        Title = display.Title;
        Artist = display.Artist;
        Album = display.Album ?? string.Empty;
        SourceLabel = display.SourceLabel;
        OriginalLyrics = display.OriginalLyrics ?? string.Empty;
        TranslatedLyrics = display.Translation ?? string.Empty;
        StatusMessage = display.Message ?? string.Empty;
        NeedsPaste = display.Status == LyricsStatus.NeedsPaste;
        NeedsApiKey = display.Status == LyricsStatus.NeedsApiKey;
        CanRetry = display.Status == LyricsStatus.Ready && display.TranslationSource == LyricsSource.Ai;
        IsBusy = display.Status == LyricsStatus.Loading;

        _track = display.Status is LyricsStatus.Ready or LyricsStatus.NeedsApiKey
            ? LyricTrack.Build(display.OriginalLyrics, display.Translation, display.SyncedLyrics)
            : [];
        RebuildLines();
        var timed = _track.Any(l => l.Timestamp is not null);
        SyncCaption = _track.Count == 0
            ? string.Empty
            : timed
                ? "依 LRC 時間軸跟隨"
                : "無 LRC 時間軸（不依等分時長猜測）";
        HighlightCurrentLine();
    }

    private void RebuildLines()
    {
        LyricLines.Clear();
        foreach (var line in _track)
        {
            LyricLines.Add(new LyricLineItem
            {
                Original = line.Original,
                Translation = line.Translation,
            });
        }

        HasLyricLines = LyricLines.Count > 0;
        CurrentLineIndex = -1;
        NotifyOverlayVisibility();
    }

    private void HighlightCurrentLine()
    {
        if (LyricLines.Count == 0)
        {
            OverlayLines.Clear();
            if (CurrentLineIndex != 0)
            {
                CurrentLineIndex = 0;
            }

            NotifyOverlayVisibility();
            return;
        }

        var index = LyricTrack.IndexAt(_track, _position);
        var changed = index != CurrentLineIndex;
        CurrentLineIndex = index;
        for (var i = 0; i < LyricLines.Count; i++)
        {
            LyricLines[i].ApplyWindow(Math.Abs(i - index));
        }

        RebuildOverlaySlice(index);
        if (changed)
        {
            CurrentLineChanged?.Invoke(this, index);
        }
    }

    private void RebuildOverlaySlice(int index)
    {
        OverlayLines.Clear();
        if (LyricLines.Count == 0)
        {
            return;
        }

        var start = Math.Max(0, index - 2);
        var end = Math.Min(LyricLines.Count - 1, index + 2);
        for (var i = start; i <= end; i++)
        {
            var source = LyricLines[i];
            var item = new LyricLineItem
            {
                Original = source.Original,
                Translation = source.Translation,
            };
            item.ApplyWindow(Math.Abs(i - index));
            OverlayLines.Add(item);
        }
    }

    partial void OnHasLyricLinesChanged(bool value) => NotifyOverlayVisibility();

    partial void OnOverlayEnabledChanged(bool value)
    {
        _settings.SetOverlayEnabled(value);
        NotifyOverlayVisibility();
    }

    private void NotifyOverlayVisibility() => OnPropertyChanged(nameof(OverlayShouldShow));

    private static string FormatPosition(TimeSpan position, TimeSpan? duration)
    {
        static string Fmt(TimeSpan value) =>
            value.TotalHours >= 1
                ? value.ToString(@"h\:mm\:ss")
                : value.ToString(@"m\:ss");

        return duration is { TotalMilliseconds: > 0 }
            ? $"{Fmt(position)} / {Fmt(duration.Value)}"
            : Fmt(position);
    }
}
