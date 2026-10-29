namespace SignalChat.Api.Hubs;

/// <summary>Sliding-window limit on how many messages one user may send. Limits live in <see cref="ChatHubLimits"/>.</summary>
public interface IMessageRateLimiter
{
    /// <summary>Takes one permit for the user; false when the window is already full.</summary>
    bool TryAcquire(Guid userId);
}
