namespace LyricsTranslator.Core.NowPlaying;

/// <summary>
/// Projects a (possibly stale) SMTC timeline sample forward using playback rate
/// and last-updated, without inventing a duration-ratio position.
/// </summary>
public static class PlaybackClock
{
    public const double DefaultRate = 1.0;

    /// <summary>
    /// SMTC LastUpdatedTime is sometimes epoch or hours off; ignore those samples.
    /// </summary>
    public static readonly TimeSpan MaxLastUpdatedAge = TimeSpan.FromSeconds(2);

    public static TimeSpan Project(
        TimeSpan reportedPosition,
        bool isPlaying,
        double playbackRate,
        DateTimeOffset? lastUpdated,
        DateTimeOffset now)
    {
        var position = NonNegative(reportedPosition);
        if (!isPlaying)
        {
            return position;
        }

        var rate = playbackRate > 0 ? playbackRate : DefaultRate;
        if (lastUpdated is not { } updated)
        {
            return position;
        }

        var elapsed = now - updated;
        if (elapsed <= TimeSpan.Zero || elapsed > MaxLastUpdatedAge)
        {
            return position;
        }

        return NonNegative(position + Scale(elapsed, rate));
    }

    /// <summary>
    /// Playhead = interpolated SMTC sample + user offset. Call on each SMTC tick.
    /// </summary>
    public static TimeSpan Playhead(
        PlaybackInterpolator clock,
        PlaybackProgress progress,
        TimeSpan userOffset,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var interpolated = clock.Update(progress, now);
        return ApplyOffset(interpolated, userOffset, progress.Duration);
    }

    /// <summary>
    /// Continue the last sample at playback rate when SMTC Position is stale.
    /// Overlay should poll this about every 100ms.
    /// </summary>
    public static TimeSpan PlayheadNow(
        PlaybackInterpolator clock,
        TimeSpan userOffset,
        TimeSpan? duration,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return ApplyOffset(clock.Current(now), userOffset, duration);
    }

    public static TimeSpan ApplyOffset(TimeSpan position, TimeSpan offset, TimeSpan? duration)
    {
        var value = position + offset;
        if (value < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        if (duration is { TotalMilliseconds: > 0 } && value > duration.Value)
        {
            return duration.Value;
        }

        return value;
    }

    public static TimeSpan Scale(TimeSpan elapsed, double rate)
    {
        if (elapsed <= TimeSpan.Zero || rate == 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromTicks((long)(elapsed.Ticks * rate));
    }

    public static TimeSpan NonNegative(TimeSpan value) =>
        value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
