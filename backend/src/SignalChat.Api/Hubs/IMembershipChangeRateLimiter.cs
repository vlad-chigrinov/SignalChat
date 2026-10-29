namespace SignalChat.Api.Hubs;

/// <summary>
/// Limit on <c>JoinRoom</c> and <c>LeaveRoom</c> calls per user, counted across all of the user's connections.
/// Checked before the membership, so probing foreign ids is limited too.
/// </summary>
public interface IMembershipChangeRateLimiter
{
    /// <summary>Takes one permit for the user; false when the window is already full.</summary>
    bool TryAcquire(Guid userId);
}
