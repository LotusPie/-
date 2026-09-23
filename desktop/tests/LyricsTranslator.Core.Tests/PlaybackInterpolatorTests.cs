using LyricsTranslator.Core.NowPlaying;

namespace LyricsTranslator.Core.Tests;

public class PlaybackInterpolatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(3);

    [Fact]
    public void Project_advances_from_last_updated_at_playback_rate()
    {
        var projected = PlaybackClock.Project(
            TimeSpan.FromSeconds(10),
            isPlaying: true,
            playbackRate: 1,
            lastUpdated: T0.AddMilliseconds(-200),
            now: T0);

        Assert.InRange(projected.TotalMilliseconds, 10_190, 10_210);
    }

    [Fact]
    public void Project_scales_elapsed_by_playback_rate()
    {
        var projected = PlaybackClock.Project(
            TimeSpan.FromSeconds(10),
            isPlaying: true,
            playbackRate: 2,
            lastUpdated: T0.AddMilliseconds(-200),
            now: T0);

        Assert.InRange(projected.TotalMilliseconds, 10_390, 10_410);
    }

    [Fact]
    public void Project_ignores_stale_last_updated()
    {
        var projected = PlaybackClock.Project(
            TimeSpan.FromSeconds(10),
            isPlaying: true,
            playbackRate: 1,
            lastUpdated: T0.AddHours(-3),
            now: T0);

        Assert.Equal(TimeSpan.FromSeconds(10), projected);
    }

    [Fact]
    public void Project_does_not_advance_while_paused()
    {
        var projected = PlaybackClock.Project(
            TimeSpan.FromSeconds(10),
            isPlaying: false,
            playbackRate: 1,
            lastUpdated: T0.AddMilliseconds(-500),
            now: T0);

        Assert.Equal(TimeSpan.FromSeconds(10), projected);
    }

    [Fact]
    public void ApplyOffset_shifts_and_clamps()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(11.5),
            PlaybackClock.ApplyOffset(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1.5), Duration));
        Assert.Equal(
            TimeSpan.Zero,
            PlaybackClock.ApplyOffset(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(-5), Duration));
        Assert.Equal(
            TimeSpan.FromSeconds(20),
            PlaybackClock.ApplyOffset(TimeSpan.FromSeconds(18), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20)));
    }

    [Fact]
    public void Interpolator_advances_while_reported_position_is_stale()
    {
        var clock = new PlaybackInterpolator();
        clock.Update(Playing(TimeSpan.FromSeconds(10), T0), T0);

        var later = clock.Update(Playing(TimeSpan.FromSeconds(10), T0), T0.AddMilliseconds(500));

        Assert.InRange(later.TotalMilliseconds, 10_490, 10_510);
    }

    [Fact]
    public void Interpolator_freezes_when_paused()
    {
        var clock = new PlaybackInterpolator();
        clock.Update(Playing(TimeSpan.FromSeconds(10), T0), T0);
        clock.Update(Paused(TimeSpan.FromSeconds(10)), T0.AddMilliseconds(200));

        var frozen = clock.Update(Paused(TimeSpan.FromSeconds(10)), T0.AddSeconds(2));

        Assert.InRange(frozen.TotalMilliseconds, 10_190, 10_210);
    }

    [Fact]
    public void Interpolator_reanchors_on_seek()
    {
        var clock = new PlaybackInterpolator();
        clock.Update(Playing(TimeSpan.FromSeconds(10), T0), T0);

        var afterSeek = clock.Update(
            Playing(TimeSpan.FromSeconds(40), T0.AddMilliseconds(200)),
            T0.AddMilliseconds(200));

        Assert.InRange(afterSeek.TotalMilliseconds, 39_900, 40_100);
    }

    [Fact]
    public void Interpolator_uses_last_updated_on_first_sample()
    {
        var clock = new PlaybackInterpolator();
        var pos = clock.Update(
            Playing(TimeSpan.FromSeconds(10), T0.AddMilliseconds(-150)),
            T0);

        Assert.InRange(pos.TotalMilliseconds, 10_140, 10_160);
    }

    private static PlaybackProgress Playing(TimeSpan position, DateTimeOffset lastUpdated) =>
        new(position, Duration, IsPlaying: true, PlaybackRate: 1, LastUpdated: lastUpdated);

    private static PlaybackProgress Paused(TimeSpan position) =>
        new(position, Duration, IsPlaying: false, PlaybackRate: 1, LastUpdated: T0);
}
