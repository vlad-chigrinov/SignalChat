namespace SignalChat.Api.Hubs;

/// <summary>
/// In-memory sliding window of <see cref="Permits"/> events per key, for one limit (one instance per limit).
/// Time comes from <see cref="TimeProvider"/> so tests can move it. Stale timestamps are dropped on every call;
/// idle keys are swept every <see cref="SweepEveryCalls"/> calls.
/// </summary>
public sealed class SlidingWindowRateLimiter(TimeProvider timeProvider, int permits, TimeSpan window)
{
    private const int SweepEveryCalls = 1000;

    private readonly object _gate = new();
    private readonly Dictionary<Guid, Queue<DateTimeOffset>> _eventsByKey = new();
    private int _callsSinceSweep;

    /// <summary>Takes one permit for the key; false when the window is already full.</summary>
    public bool TryAcquire(Guid key)
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (++_callsSinceSweep >= SweepEveryCalls)
            {
                _callsSinceSweep = 0;
                SweepIdleKeys(now);
            }

            if (!_eventsByKey.TryGetValue(key, out var events))
            {
                events = new Queue<DateTimeOffset>();
                _eventsByKey[key] = events;
            }

            DropExpired(events, now);
            if (events.Count >= permits)
            {
                return false;
            }

            events.Enqueue(now);
            return true;
        }
    }

    private void DropExpired(Queue<DateTimeOffset> events, DateTimeOffset now)
    {
        while (events.Count > 0 && now - events.Peek() >= window)
        {
            events.Dequeue();
        }
    }

    private void SweepIdleKeys(DateTimeOffset now)
    {
        foreach (var key in _eventsByKey.Keys.ToList())
        {
            var events = _eventsByKey[key];
            DropExpired(events, now);
            if (events.Count == 0)
            {
                _eventsByKey.Remove(key);
            }
        }
    }
}
