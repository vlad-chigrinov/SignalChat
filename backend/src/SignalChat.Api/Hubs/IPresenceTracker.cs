namespace SignalChat.Api.Hubs;

/// <summary>
/// Counts live connections per user. A user is online while at least one connection is open, so several
/// tabs produce one online and one offline transition. Interface kept so a shared store can replace it later.
/// </summary>
public interface IPresenceTracker
{
    /// <summary>Registers a connection; true when it is the user's first one (the user just came online).</summary>
    bool ConnectionOpened(Guid userId);

    /// <summary>Unregisters a connection; true when it was the user's last one (the user just went offline).</summary>
    bool ConnectionClosed(Guid userId);

    bool IsOnline(Guid userId);
}
