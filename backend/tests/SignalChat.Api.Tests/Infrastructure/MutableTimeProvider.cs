namespace SignalChat.Api.Tests.Infrastructure;

/// <summary>
/// A clock tests move forward by hand, to exercise lifetimes without waiting. Timers created through it fire only
/// when <see cref="Advance"/> passes their due time, so a test decides exactly when a grace period ends.
/// </summary>
public sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (period != Timeout.InfiniteTimeSpan)
        {
            throw new NotSupportedException("Test timers are one-shot only.");
        }

        var timer = new ManualTimer(callback, state, _now + dueTime, Remove);
        lock (_gate)
        {
            _timers.Add(timer);
        }
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        _now += by;
        foreach (var timer in TakeDueTimers())
        {
            timer.Callback(timer.State);
        }
    }

    private List<ManualTimer> TakeDueTimers()
    {
        lock (_gate)
        {
            var due = _timers.Where(timer => timer.DueAt <= _now).ToList();
            _timers.RemoveAll(timer => timer.DueAt <= _now);
            return due;
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }
}
