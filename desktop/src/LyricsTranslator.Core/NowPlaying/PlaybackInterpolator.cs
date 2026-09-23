namespace LyricsTranslator.Core.NowPlaying;

/// <summary>
/// Local playhead between sparse SMTC ticks. When Position is unchanged the
/// clock keeps advancing at PlaybackRate instead of freezing on line 0.
/// </summary>
public sealed class PlaybackInterpolator
{
    public static readonly TimeSpan SeekThreshold = TimeSpan.FromMilliseconds(500);

    private readonly object _gate = new();
    private TimeSpan _lastReported;
    private TimeSpan _anchorPosition;
    private DateTimeOffset _anchorTime;
    private bool _playing;
    private double _rate = PlaybackClock.DefaultRate;
    private TimeSpan? _duration;
    private bool _hasSample;

    public TimeSpan Update(PlaybackProgress progress, DateTimeOffset now)
    {
        lock (_gate)
        {
            var rate = progress.PlaybackRate > 0 ? progress.PlaybackRate : PlaybackClock.DefaultRate;
            var projected = PlaybackClock.Project(
                progress.Position,
                progress.IsPlaying,
                rate,
                progress.LastUpdated,
                now);

            if (!_hasSample)
            {
                Anchor(projected, now, progress, rate);
                return Cap(projected);
            }

            var reportedDelta = (progress.Position - _lastReported).Duration();
            var reportedMoved = reportedDelta > TimeSpan.FromMilliseconds(20);
            var seeked = reportedDelta >= SeekThreshold;

            if (!progress.IsPlaying)
            {
                var freezeAt = reportedMoved ? projected : Predict(now);
                Anchor(freezeAt, now, progress, rate);
                _playing = false;
                return Cap(freezeAt);
            }

            if (seeked)
            {
                Anchor(projected, now, progress, rate);
                return Cap(projected);
            }

            if (reportedMoved)
            {
                Anchor(projected, now, progress, rate);
                return Cap(projected);
            }

            if (Math.Abs(_rate - rate) > 0.01)
            {
                var current = Predict(now);
                Anchor(current, now, progress, rate);
                return Cap(current);
            }

            _playing = true;
            _rate = rate;
            _duration = progress.Duration;
            _lastReported = progress.Position;
            return Cap(Predict(now));
        }
    }

    public TimeSpan Current(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_hasSample)
            {
                return TimeSpan.Zero;
            }

            return Cap(Predict(now));
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _hasSample = false;
            _lastReported = TimeSpan.Zero;
            _anchorPosition = TimeSpan.Zero;
            _playing = false;
            _rate = PlaybackClock.DefaultRate;
            _duration = null;
        }
    }

    private TimeSpan Predict(DateTimeOffset now)
    {
        if (!_playing)
        {
            return _anchorPosition;
        }

        var elapsed = now - _anchorTime;
        if (elapsed <= TimeSpan.Zero)
        {
            return _anchorPosition;
        }

        return _anchorPosition + PlaybackClock.Scale(elapsed, _rate);
    }

    private void Anchor(TimeSpan position, DateTimeOffset now, PlaybackProgress progress, double rate)
    {
        _anchorPosition = PlaybackClock.NonNegative(position);
        _anchorTime = now;
        _playing = progress.IsPlaying;
        _rate = rate;
        _duration = progress.Duration;
        _lastReported = progress.Position;
        _hasSample = true;
    }

    private TimeSpan Cap(TimeSpan position)
    {
        position = PlaybackClock.NonNegative(position);
        if (_duration is { TotalMilliseconds: > 0 } && position > _duration.Value)
        {
            return _duration.Value;
        }

        return position;
    }
}
