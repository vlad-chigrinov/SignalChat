namespace SignalChat.Api.Hubs;

/// <summary>Connection counter per user, held in memory (singleton). One lock keeps the counts exact.</summary>
public sealed class InMemoryPresenceTracker : IPresenceTracker
{
    private const int NoConnections = 0;

    private readonly object _gate = new();
    private readonly Dictionary<Guid, int> _connectionsByUser = new();

    public bool ConnectionOpened(Guid userId)
    {
        lock (_gate)
        {
            _connectionsByUser.TryGetValue(userId, out var open);
            _connectionsByUser[userId] = open + 1;
            return open == NoConnections;
        }
    }

    public bool ConnectionClosed(Guid userId)
    {
        lock (_gate)
        {
            if (!_connectionsByUser.TryGetValue(userId, out var open))
            {
                return false;
            }

            if (open > 1)
            {
                _connectionsByUser[userId] = open - 1;
                return false;
            }

            _connectionsByUser.Remove(userId);
            return true;
        }
    }

    public bool IsOnline(Guid userId)
    {
        lock (_gate)
        {
            return _connectionsByUser.ContainsKey(userId);
        }
    }
}
