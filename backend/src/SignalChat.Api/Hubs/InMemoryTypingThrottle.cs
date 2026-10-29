namespace SignalChat.Api.Hubs;

/// <summary>
/// In-memory typing throttle (singleton). Time comes from <see cref="TimeProvider"/>. Expired entries are swept
/// every <see cref="SweepEveryCalls"/> calls so the map does not grow with every pair ever seen.
/// </summary>
public sealed class InMemoryTypingThrottle(TimeProvider timeProvider) : ITypingThrottle
{
    private const int SweepEveryCalls = 1000;

    private readonly object _gate = new();
    private readonly Dictionary<(Guid UserId, Guid ConversationId), DateTimeOffset> _lastAcceptedAt = new();
    private int _callsSinceSweep;

    public bool TryPass(Guid userId, Guid conversationId)
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (++_callsSinceSweep >= SweepEveryCalls)
            {
                _callsSinceSweep = 0;
                Sweep(now);
            }

            var key = (userId, conversationId);
            if (_lastAcceptedAt.TryGetValue(key, out var lastAcceptedAt)
                && now - lastAcceptedAt < ChatHubLimits.TypingThrottle)
            {
                return false;
            }

            _lastAcceptedAt[key] = now;
            return true;
        }
    }

    private void Sweep(DateTimeOffset now)
    {
        foreach (var entry in _lastAcceptedAt.Where(entry => now - entry.Value >= ChatHubLimits.TypingThrottle).ToList())
        {
            _lastAcceptedAt.Remove(entry.Key);
        }
    }
}
