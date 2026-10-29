namespace SignalChat.Api.Hubs;

/// <summary>
/// In-memory sliding window per user (singleton). Time comes from <see cref="TimeProvider"/> so tests can move it.
/// Stale timestamps are dropped on every call; idle users are swept every <see cref="SweepEveryCalls"/> calls.
/// </summary>
public sealed class SlidingWindowMessageRateLimiter(TimeProvider timeProvider) : IMessageRateLimiter
{
    private const int SweepEveryCalls = 1000;

    private readonly object _gate = new();
    private readonly Dictionary<Guid, Queue<DateTimeOffset>> _sendsByUser = new();
    private int _callsSinceSweep;

    public bool TryAcquire(Guid userId)
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (++_callsSinceSweep >= SweepEveryCalls)
            {
                _callsSinceSweep = 0;
                SweepIdleUsers(now);
            }

            if (!_sendsByUser.TryGetValue(userId, out var sends))
            {
                sends = new Queue<DateTimeOffset>();
                _sendsByUser[userId] = sends;
            }

            DropExpired(sends, now);
            if (sends.Count >= ChatHubLimits.SendRateLimitPermits)
            {
                return false;
            }

            sends.Enqueue(now);
            return true;
        }
    }

    private static void DropExpired(Queue<DateTimeOffset> sends, DateTimeOffset now)
    {
        while (sends.Count > 0 && now - sends.Peek() >= ChatHubLimits.SendRateLimitWindow)
        {
            sends.Dequeue();
        }
    }

    private void SweepIdleUsers(DateTimeOffset now)
    {
        foreach (var userId in _sendsByUser.Keys.ToList())
        {
            var sends = _sendsByUser[userId];
            DropExpired(sends, now);
            if (sends.Count == 0)
            {
                _sendsByUser.Remove(userId);
            }
        }
    }
}
